using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Onyx.Application;
using Onyx.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// The only HTTP surface is an internal health endpoint, bound to loopback by default (Worker:HealthUrls).
builder.WebHost.UseUrls(builder.Configuration["Worker:HealthUrls"] ?? "http://127.0.0.1:5290");
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// Composition root: the only place Onyx.Worker touches Onyx.Infrastructure.
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddWorkerProcessing(builder.Configuration);

var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.Run();
