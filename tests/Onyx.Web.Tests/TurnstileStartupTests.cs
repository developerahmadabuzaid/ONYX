using System.Net;
using Microsoft.Extensions.Options;
using Onyx.Web.Tests.Infrastructure;

namespace Onyx.Web.Tests;

/// <summary>Onyx.Web in Production needs the Turnstile site key; other environments render forms as unavailable without it.</summary>
public sealed class TurnstileStartupTests
{
    [Fact]
    public async Task Production_startup_fails_without_a_site_key()
    {
        await using var factory = new OnyxWebFactory { Environment = "Production" };
        factory.Settings["Turnstile:SiteKey"] = string.Empty;

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Production requires Turnstile:SiteKey", Describe(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_starts_with_a_site_key_and_development_without_one()
    {
        await using (var production = new OnyxWebFactory { Environment = "Production" })
        {
            using var client = production.CreateBrowser();
            Assert.Equal(HttpStatusCode.OK, (await client.GetPathAsync("/admin/account/login")).StatusCode);
        }

        await using var development = new OnyxWebFactory { Environment = "Development" };
        development.Settings["Turnstile:SiteKey"] = string.Empty;
        using var developmentClient = development.CreateBrowser();
        Assert.Equal(HttpStatusCode.OK, (await developmentClient.GetPathAsync("/admin/account/login")).StatusCode);
    }

    private static string Describe(Exception? exception)
    {
        var messages = new List<string>();
        while (exception is not null)
        {
            messages.Add(exception.Message);
            if (exception is OptionsValidationException validation)
            {
                messages.AddRange(validation.Failures);
            }

            exception = exception.InnerException;
        }

        return string.Join(" | ", messages);
    }
}
