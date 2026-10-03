using Microsoft.AspNetCore.Mvc.Testing;

namespace Onyx.Web.Tests;

public sealed class WebHostSmokeTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Web_host_starts_and_handles_requests()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // No routes exist until Stage 4; the point is that the host boots and the pipeline responds.
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
