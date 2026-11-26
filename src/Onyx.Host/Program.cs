using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Onyx.API.Configuration;
using Onyx.Application;
using Onyx.Infrastructure;
using Onyx.Web.Configuration;
using Serilog;

namespace Onyx.Host;

/// <summary>
/// Runs the whole site as one application (one process, one IIS site). The API is started first as an internal server on a
/// free loopback port (never reachable from outside, no IIS integration); the public website then starts with
/// <c>OnyxApi:BaseUrl</c> pointing at it. Both apps keep their own pipelines, authentication schemes and settings, so their
/// behaviour, security boundaries and tests are unchanged. Optionally (<c>Host:RunWorker</c>) the Worker's background jobs
/// run inside the internal API app too.
/// </summary>
public static class HostProgram
{
    public const string RunWorkerKey = "Host:RunWorker";

    public static async Task Main(string[] args)
    {
        var api = BuildApi(args);

        // Operator commands (seed-identity, bootstrap-superadmin, reset-two-factor) run instead of the site.
        if (await AdminCommandRunner.TryRunAsync(api, args))
        {
            return;
        }

        await api.StartAsync();
        try
        {
            var apiAddress = api.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var web = BuildWeb(args, apiAddress);
            await web.RunAsync();
        }
        finally
        {
            await api.StopAsync();
            await api.DisposeAsync();
        }
    }

    private static WebApplication BuildApi(string[] args)
    {
        // The slim builder has no IIS integration, so this inner server never competes with the public site for IIS.
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = args, ApplicationName = "Onyx.API" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Reached only by the website in this process, through loopback.
            ["AllowedHosts"] = "127.0.0.1;localhost",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        builder.WebHost.ConfigureKestrel((context, kestrel) =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize =
                context.Configuration.GetValue<long?>($"{ApiSecurityOptions.SectionName}:{nameof(ApiSecurityOptions.MaxRequestBodySizeBytes)}")
                ?? new ApiSecurityOptions().MaxRequestBodySizeBytes;
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);

        // Before the API: the Worker registers system-only ICurrentUser/IRequestContext, which the API's request-aware versions
        // must replace (last registration wins). Background jobs then see no signed-in user, exactly as in Onyx.Worker.
        if (builder.Configuration.GetValue<bool>(RunWorkerKey))
        {
            builder.Services.AddWorkerProcessing(builder.Configuration);
        }

        builder.Services
            .AddOnyxIdentity(builder.Configuration)
            .RequireTurnstileInProduction()
            .AddOnyxApi();

        var app = builder.Build();
        app.UseOnyxApi();
        return app;
    }

    private static WebApplication BuildWeb(string[] args, string apiAddress)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = "Onyx.Web" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OnyxApi:BaseUrl"] = apiAddress });
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
        builder.Services.AddOnyxWeb(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.UseOnyxWeb();
        return app;
    }
}
