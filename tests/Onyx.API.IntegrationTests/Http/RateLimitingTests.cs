using System.Net;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Global_limit_returns_429_problem_with_retry_after()
    {
        await using var factory = CreateFactory(globalLimit: 3, strictLimit: 100);
        using var client = factory.CreateClient();
        var ping = new Uri("/api/v1/test-only/ping", UriKind.Relative);

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.GetAsync(ping, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var rejected = await client.GetAsync(ping, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(rejected.Headers.RetryAfter);
    }

    [Fact]
    public async Task Strict_policy_applies_its_own_tighter_limit()
    {
        await using var factory = CreateFactory(globalLimit: 100, strictLimit: 2);
        using var client = factory.CreateClient();
        var strict = new Uri("/api/v1/test-only/strict", UriKind.Relative);

        using var first = await client.GetAsync(strict, TestContext.Current.CancellationToken);
        using var second = await client.GetAsync(strict, TestContext.Current.CancellationToken);
        using var third = await client.GetAsync(strict, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_are_not_rate_limited()
    {
        await using var factory = CreateFactory(globalLimit: 1, strictLimit: 1);
        using var client = factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static OnyxApiFactory CreateFactory(int globalLimit, int strictLimit) =>
        new("Testing", OnyxApiFactory.UnreachableDatabase, new Dictionary<string, string?>
        {
            ["RateLimiting:Global:PermitLimit"] = globalLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:Global:WindowSeconds"] = "60",
            ["RateLimiting:Strict:PermitLimit"] = strictLimit.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["RateLimiting:Strict:WindowSeconds"] = "60",
        });
}
