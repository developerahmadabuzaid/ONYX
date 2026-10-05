using System.Net;
using Microsoft.Extensions.Options;
using Onyx.API.IntegrationTests.Infrastructure;

namespace Onyx.API.IntegrationTests.Http;

/// <summary>Production refuses to start without Turnstile; Development and Testing run without keys.</summary>
public sealed class TurnstileStartupTests
{
    [Fact]
    public async Task Production_startup_fails_without_a_turnstile_secret()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase, new Dictionary<string, string?> { ["Turnstile:SecretKey"] = string.Empty });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        var validation = FindInner<OptionsValidationException>(exception);
        Assert.NotNull(validation);
        Assert.Contains("Production requires Turnstile", validation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_starts_with_a_secret_and_a_hostname()
    {
        await using var factory = new OnyxApiFactory("Production", OnyxApiFactory.UnreachableDatabase, new Dictionary<string, string?>
        {
            ["Turnstile:SecretKey"] = "test-placeholder-secret",
            ["Turnstile:AllowedHostnames:0"] = "www.onyx.test",
        });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Development_and_testing_run_without_turnstile_keys(string environment)
    {
        await using var factory = new OnyxApiFactory(environment, OnyxApiFactory.UnreachableDatabase, new Dictionary<string, string?> { ["Turnstile:SecretKey"] = string.Empty });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken)).StatusCode);
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

            if (exception is AggregateException aggregate && aggregate.InnerExceptions.Select(FindInner<T>).FirstOrDefault(found => found is not null) is { } nested)
            {
                return nested;
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
