using System.Net;
using Google;
using Microsoft.Extensions.Options;
using Onyx.Application.Analytics;
using Onyx.Infrastructure.Analytics;

namespace Onyx.API.IntegrationTests.Analytics;

/// <summary>The Google adapters' boundary without any network call: configuration rules, not-configured behaviour, safe error codes.</summary>
public sealed class GoogleAdapterTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("123456789", true)]
    [InlineData("properties/123", false)]
    [InlineData("12a", false)]
    [InlineData("", false)]
    public void Ga4_property_ids_are_digits_only(string value, bool valid) => Assert.Equal(valid, GoogleAnalyticsOptions.IsValidPropertyId(value));

    [Theory]
    [InlineData("sc-domain:onyx.example.com", true)]
    [InlineData("https://www.onyx.example.com/", true)]
    [InlineData("https://www.onyx.example.com", false)]
    [InlineData("http://www.onyx.example.com/", false)]
    [InlineData("https://user:pass@www.onyx.example.com/", false)]
    [InlineData("sc-domain:localhost", false)]
    [InlineData("onyx.example.com", false)]
    public void Search_console_properties_are_domain_or_https_prefix_properties(string value, bool valid) =>
        Assert.Equal(valid, GoogleAnalyticsOptions.IsValidSiteUrl(value));

    [Fact]
    public async Task Without_credentials_the_adapters_are_not_configured_and_never_call_out()
    {
        var account = new GoogleServiceAccount(Options.Create(new GoogleAnalyticsOptions { Ga4PropertyId = "123456789", SearchConsoleSiteUrl = "sc-domain:onyx.example.com" }));
        var ga4 = new GoogleAnalyticsDataProvider(account);
        var gsc = new GoogleSearchConsoleProvider(account);
        var request = new AnalyticsFetchRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), 100, ConversionEvents.All);

        Assert.False(ga4.IsConfigured);
        Assert.False(gsc.IsConfigured);
        Assert.Equal(AnalyticsProviderErrors.NotConfigured, (await ga4.FetchAsync(request, Token)).Error!.Code);
        Assert.Equal(AnalyticsProviderErrors.NotConfigured, (await gsc.FetchAsync(request, Token)).Error!.Code);
    }

    [Fact]
    public async Task An_invalid_key_fails_as_unauthorized_without_echoing_the_secret()
    {
        const string key = """{"type":"external_account","audience":"secret-audience-value","credential_source":{"url":"http://169.254.169.254/"}}""";
        var account = new GoogleServiceAccount(Options.Create(new GoogleAnalyticsOptions { Ga4PropertyId = "123456789", ServiceAccountKeyJson = key }));
        var ga4 = new GoogleAnalyticsDataProvider(account);

        var result = await ga4.FetchAsync(new AnalyticsFetchRequest(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2), 100, ConversionEvents.All), Token);

        Assert.True(ga4.IsConfigured);
        Assert.Equal(AnalyticsProviderErrors.Unauthorized, result.Error!.Code);   // only service-account keys are accepted
        Assert.DoesNotContain("secret-audience-value", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_failures_map_to_safe_codes()
    {
        Assert.Equal(AnalyticsProviderErrors.Unauthorized, Map(new GoogleApiException("analyticsdata", "denied") { HttpStatusCode = HttpStatusCode.Forbidden }));
        Assert.Equal(AnalyticsProviderErrors.QuotaExceeded, Map(new GoogleApiException("analyticsdata", "quota") { HttpStatusCode = HttpStatusCode.TooManyRequests }));
        Assert.Equal(AnalyticsProviderErrors.Unavailable, Map(new GoogleApiException("analyticsdata", "boom") { HttpStatusCode = HttpStatusCode.InternalServerError }));
        Assert.Equal(AnalyticsProviderErrors.Unavailable, Map(new HttpRequestException("dns")));
        Assert.Equal(AnalyticsProviderErrors.Timeout, Map(new TaskCanceledException()));
        Assert.Equal("The analytics provider request failed.", GoogleErrors.Map(new HttpRequestException("secret detail")).Message);

        static string Map(Exception exception) => GoogleErrors.Map(exception).Code;
    }
}
