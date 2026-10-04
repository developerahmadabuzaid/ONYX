using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Onyx.Application.Providers;
using Onyx.Application.Seo;
using Onyx.Domain.Providers;
using Onyx.Infrastructure.Seo;

namespace Onyx.API.IntegrationTests.Seo;

/// <summary>Unit tests for the IndexNow adapter against a fake endpoint (no network, no database).</summary>
public sealed class IndexNowNotifierTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly FakeEndpoint _endpoint = new();
    private readonly FakeProviders _providers = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _endpoint.Dispose();

    [Theory]
    [InlineData(false, "https://www.onyx.test", Key)]
    [InlineData(true, "https://www.onyx.test", "")]
    [InlineData(true, "https://www.onyx.test", "short")]
    [InlineData(true, "http://www.onyx.test", Key)]
    [InlineData(true, "", Key)]
    public async Task It_stays_inactive_until_configured_and_launched(bool allowIndexing, string baseUrl, string key)
    {
        using var notifier = Create(allowIndexing, baseUrl, key);

        await notifier.Notifier.NotifyAsync(["/en/about"], Token);

        Assert.False(notifier.Notifier.IsActive);
        Assert.Null(notifier.Notifier.VerificationKey);
        Assert.Empty(_endpoint.Requests);
    }

    [Fact]
    public async Task It_submits_encoded_urls_of_this_host_with_the_key_location()
    {
        using var notifier = Create();

        await notifier.Notifier.NotifyAsync(["/ar/الخدمات/حلول", "/en/services", "/en/services", "/ar/", "//evil.example/x", "https://evil.example/y"], Token);

        var body = JsonDocument.Parse(Assert.Single(_endpoint.Requests)).RootElement;
        Assert.Equal("www.onyx.test", body.GetProperty("host").GetString());
        Assert.Equal(Key, body.GetProperty("key").GetString());
        Assert.Equal("https://www.onyx.test/indexnow-key.txt", body.GetProperty("keyLocation").GetString());
        var urls = body.GetProperty("urlList").EnumerateArray().Select(url => url.GetString()).ToList();
        Assert.Equal(
            ["https://www.onyx.test/ar/" + Uri.EscapeDataString("الخدمات") + "/" + Uri.EscapeDataString("حلول"), "https://www.onyx.test/en/services", "https://www.onyx.test/ar/"],
            urls);
        Assert.Equal(Key, notifier.Notifier.VerificationKey);
        Assert.Equal(ProviderConnectionStatus.Active, _providers.Last);
    }

    [Fact]
    public async Task Large_changes_are_split_into_batches_of_ten_thousand()
    {
        using var notifier = Create();

        await notifier.Notifier.NotifyAsync(Enumerable.Range(0, 10_001).Select(index => $"/en/page-{index}").ToList(), Token);

        Assert.Equal(2, _endpoint.Requests.Count);
    }

    [Fact]
    public async Task Rate_limits_and_server_errors_are_retried_later()
    {
        using var notifier = Create();
        _endpoint.Status = HttpStatusCode.TooManyRequests;

        await Assert.ThrowsAsync<SearchEngineNotificationDeferredException>(() => notifier.Notifier.NotifyAsync(["/en/a"], Token));
        Assert.Equal(ProviderConnectionStatus.Error, _providers.Last);

        _endpoint.Status = HttpStatusCode.ServiceUnavailable;
        await Assert.ThrowsAsync<SearchEngineNotificationDeferredException>(() => notifier.Notifier.NotifyAsync(["/en/a"], Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Configuration_errors_are_reported_without_endless_retries(HttpStatusCode status)
    {
        using var notifier = Create();
        _endpoint.Status = status;

        await notifier.Notifier.NotifyAsync(["/en/a"], Token);

        Assert.Equal(ProviderConnectionStatus.Error, _providers.Last);
        Assert.DoesNotContain(Key, _providers.LastError ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_event_handler_does_nothing_while_inactive()
    {
        using var notifier = Create(allowIndexing: false);
        var handler = new PublicUrlsChangedHandler(notifier.Notifier);

        await handler.HandleAsync(new Domain.Content.PublicUrlsChanged(Guid.NewGuid(), ["/en/a"]), Token);

        Assert.Empty(_endpoint.Requests);
    }

    private TestNotifier Create(bool allowIndexing = true, string baseUrl = "https://www.onyx.test", string key = Key)
    {
        var client = new HttpClient(_endpoint, disposeHandler: false);
        var notifier = new IndexNowNotifier(
            client,
            Options.Create(new IndexNowOptions { Key = key, Endpoint = "https://api.indexnow.test/indexnow" }),
            Options.Create(new PublicSiteOptions { PublicBaseUrl = baseUrl, AllowIndexing = allowIndexing }),
            _providers,
            NullLogger<IndexNowNotifier>.Instance);
        return new TestNotifier(notifier, client);
    }

    private sealed record TestNotifier(IndexNowNotifier Notifier, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private sealed class FakeEndpoint : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.indexnow.test/indexnow", request.RequestUri!.ToString());
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Status);
        }
    }

    private sealed class FakeProviders : IProviderConnectionService
    {
        public ProviderConnectionStatus? Last { get; private set; }

        public string? LastError { get; private set; }

        public Task ReportNotConfiguredAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken)
        {
            Last = ProviderConnectionStatus.NotConfigured;
            return Task.CompletedTask;
        }

        public Task ReportSuccessAsync(string providerKey, ProviderCategory category, CancellationToken cancellationToken)
        {
            Assert.Equal(ProviderKeys.IndexNow, providerKey);
            Last = ProviderConnectionStatus.Active;
            return Task.CompletedTask;
        }

        public Task ReportFailureAsync(string providerKey, ProviderCategory category, string error, CancellationToken cancellationToken)
        {
            Last = ProviderConnectionStatus.Error;
            LastError = error;
            return Task.CompletedTask;
        }
    }
}
