using System.Net;
using Microsoft.Extensions.Options;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

public sealed class HostingTests
{
    [Fact]
    public async Task Liveness_does_not_depend_on_the_database()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Readiness_reports_unhealthy_without_leaking_connection_details()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body);
    }

    [Fact]
    public async Task Startup_fails_fast_when_the_connection_string_is_missing()
    {
        await using var factory = new OnyxApiFactory("Production", connectionString: string.Empty);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = FindInner<OptionsValidationException>(exception);
        Assert.NotNull(validation);
        Assert.Contains("ConnectionStrings:Onyx", validation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApi_and_swagger_are_available_in_development()
    {
        await using var factory = new OnyxApiFactory("Development", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        using var ui = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task OpenApi_and_swagger_are_not_exposed_in_production(string path)
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static T? FindInner<T>(Exception? exception)
        where T : Exception
    {
        while (exception is not null)
        {
            if (exception is T match)
            {
                return match;
            }

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    var found = FindInner<T>(inner);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
