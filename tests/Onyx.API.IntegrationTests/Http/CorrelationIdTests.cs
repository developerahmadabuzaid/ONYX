using System.Text.RegularExpressions;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

public sealed partial class CorrelationIdTests
{
    private const string Header = "X-Correlation-ID";
    private static readonly Uri Ping = new("/api/v1/test-only/ping", UriKind.Relative);

    [Fact]
    public async Task A_correlation_id_is_generated_when_none_is_sent()
    {
        await using var factory = new OnyxApiFactory("Testing", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Ping, TestContext.Current.CancellationToken);

        Assert.Matches(GeneratedId(), response.Headers.GetValues(Header).Single());
    }

    [Fact]
    public async Task A_well_formed_inbound_correlation_id_is_echoed()
    {
        await using var factory = new OnyxApiFactory("Testing", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Ping);
        request.Headers.Add(Header, "web-req-2026-0001");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal("web-req-2026-0001", response.Headers.GetValues(Header).Single());
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("short")]
    [InlineData("a-very-long-correlation-identifier-that-exceeds-the-sixty-four-character-limit-xx")]
    public async Task Malformed_inbound_correlation_ids_are_replaced(string inbound)
    {
        await using var factory = new OnyxApiFactory("Testing", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Ping);
        request.Headers.TryAddWithoutValidation(Header, inbound);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var echoed = response.Headers.GetValues(Header).Single();
        Assert.NotEqual(inbound, echoed);
        Assert.Matches(GeneratedId(), echoed);
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex GeneratedId();
}
