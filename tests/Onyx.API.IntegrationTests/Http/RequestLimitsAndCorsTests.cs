using System.Net;
using System.Text;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

public sealed class RequestLimitsAndCorsTests
{
    private static readonly Uri Echo = new("/api/v1/test-only/echo", UriKind.Relative);

    [Fact]
    public async Task Bodies_over_the_global_limit_are_rejected_with_413()
    {
        await using var factory = CreateFactory(maxBodyBytes: 2_048);
        using var client = factory.CreateClient();
        using var content = JsonBody(new string('a', 4_096));

        using var response = await client.PostAsync(Echo, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Bodies_within_the_limit_are_accepted()
    {
        await using var factory = CreateFactory(maxBodyBytes: 2_048);
        using var client = factory.CreateClient();
        using var content = JsonBody("hello");

        using var response = await client.PostAsync(Echo, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cors_preflight_and_cross_origin_requests_get_no_allow_origin()
    {
        await using var factory = CreateFactory(maxBodyBytes: 2_048);
        using var client = factory.CreateClient();

        using var preflight = new HttpRequestMessage(HttpMethod.Options, Echo);
        preflight.Headers.Add("Origin", "https://evil.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        using var preflightResponse = await client.SendAsync(preflight, TestContext.Current.CancellationToken);

        using var crossOrigin = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/test-only/ping", UriKind.Relative));
        crossOrigin.Headers.Add("Origin", "https://evil.example");
        using var crossOriginResponse = await client.SendAsync(crossOrigin, TestContext.Current.CancellationToken);

        Assert.False(preflightResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(crossOriginResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static StringContent JsonBody(string text) =>
        new($"{{\"text\":\"{text}\"}}", Encoding.UTF8, "application/json");

    private static OnyxApiFactory CreateFactory(long maxBodyBytes) =>
        new("Testing", OnyxApiFactory.UnreachableDatabase, new Dictionary<string, string?>
        {
            ["ApiSecurity:MaxRequestBodySizeBytes"] = maxBodyBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
}
