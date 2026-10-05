using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.TestOnly;

namespace Onyx.API.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts Onyx.API in memory. The connection string is always overridden, so the app under test can only ever reach a
/// disposable test database (or an unreachable placeholder). Each factory gets its own random JWT signing key.
/// </summary>
public sealed class OnyxApiFactory(
    string environment,
    string connectionString,
    IReadOnlyDictionary<string, string?>? settings = null,
    TimeProvider? timeProvider = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    /// <summary>Points at a closed local port: lets HTTP-pipeline tests run without any database.</summary>
    public const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=onyx_unavailable;Username=none;Password=none;Timeout=2";

    public const string AdminPortalBaseUrl = "https://admin.onyx.test";

    public string SigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["IdentitySecurity:AdminPortalBaseUrl"] = AdminPortalBaseUrl,
            };

            // Production requires Turnstile at startup; hosts under test get a placeholder secret (never a real key) unless a test overrides it.
            if (environment == "Production")
            {
                values["Turnstile:SecretKey"] = "test-placeholder-secret";
            }

            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            {
                values[key] = value;
            }

            values["ConnectionStrings:Onyx"] = connectionString;
            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(TestOnlyController).Assembly);
            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }

            configureServices?.Invoke(services);
        });
    }
}
