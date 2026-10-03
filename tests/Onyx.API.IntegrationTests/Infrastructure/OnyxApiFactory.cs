using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Onyx.API.IntegrationTests.TestOnly;

namespace Onyx.API.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts Onyx.API in memory. The connection string is always overridden, so the app under test can only ever reach a
/// disposable test database (or an unreachable placeholder) — never a development or production database.
/// </summary>
public sealed class OnyxApiFactory(string environment, string connectionString, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<Program>
{
    /// <summary>Points at a closed local port: lets HTTP-pipeline tests run without any database.</summary>
    public const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=onyx_unavailable;Username=none;Password=none;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
            {
                ["ConnectionStrings:Onyx"] = connectionString,
            };
            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(TestOnlyController).Assembly));
    }
}
