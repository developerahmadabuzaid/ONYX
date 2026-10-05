using Onyx.API.Configuration;
using Onyx.Application;
using Onyx.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

// Composition root: the only place Onyx.API touches Onyx.Infrastructure.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddOnyxIdentity(builder.Configuration)
    .RequireTurnstileInProduction()
    .AddOnyxApi();

var app = builder.Build();

// Operator commands (seed-identity, bootstrap-superadmin, reset-two-factor) run instead of the web host.
if (await AdminCommandRunner.TryRunAsync(app, args))
{
    return;
}

app.UseOnyxApi();
app.Run();
