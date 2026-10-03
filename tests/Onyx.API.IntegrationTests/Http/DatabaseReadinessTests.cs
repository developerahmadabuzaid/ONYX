using System.Net;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

[Collection(DatabaseCollection.Name)]
public sealed class DatabaseReadinessTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Readiness_is_healthy_when_the_test_database_is_reachable()
    {
        fixture.RequireDatabase();
        await using var factory = new OnyxApiFactory("Production", fixture.ApplicationDatabase!.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
