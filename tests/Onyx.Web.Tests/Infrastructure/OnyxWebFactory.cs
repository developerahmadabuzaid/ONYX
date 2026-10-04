using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Onyx.Web.Tests.Infrastructure;

/// <summary>Hosts Onyx.Web in memory against <see cref="FakeOnyxApi"/>, with its own throwaway Data Protection key ring.</summary>
public sealed class OnyxWebFactory : WebApplicationFactory<Program>
{
    public const string ApiBaseUrl = "https://api.onyx.test";
    public const string PublicBaseUrl = "https://www.onyx.test";

    private readonly string _keysDirectory = Path.Combine(Path.GetTempPath(), "onyx-web-tests", Guid.NewGuid().ToString("N"));

    public FakeOnyxApi Api { get; } = new();

    /// <summary>Every log message the app writes, at all levels (used to prove secrets never reach logs).</summary>
    public CapturingLoggerProvider Logs { get; } = new();

    public ManualTimeProvider Time { get; } = new(DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

    /// <summary>A browser-like client: https, cookies kept, redirects not followed (so each hop can be asserted).</summary>
    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OnyxApi:BaseUrl"] = ApiBaseUrl,
            ["Site:PublicBaseUrl"] = PublicBaseUrl,
            ["DataProtection:KeysDirectory"] = _keysDirectory,
        }));

        // Provider-specific rule: capture every category at every level, regardless of appsettings filters.
        builder.ConfigureLogging(logging => logging.AddProvider(Logs).AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace));

        builder.ConfigureTestServices(services =>
        {
            services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => Api));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Api.Dispose();
        if (Directory.Exists(_keysDirectory))
        {
            Directory.Delete(_keysDirectory, recursive: true);
        }
    }
}
