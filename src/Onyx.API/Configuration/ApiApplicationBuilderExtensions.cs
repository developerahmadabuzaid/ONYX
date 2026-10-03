using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Onyx.API.Middleware;
using Serilog;

namespace Onyx.API.Configuration;

public static class ApiApplicationBuilderExtensions
{
    public const string LivenessPath = "/health/live";
    public const string ReadinessPath = "/health/ready";

    public static WebApplication UseOnyxApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Must run first so RemoteIpAddress/scheme reflect trusted proxies only.
        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseMiddleware<RequestBodySizeLimitMiddleware>();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Onyx API v1"));
        }

        app.UseRouting();

        // Rate limiting runs before authentication so brute-force traffic is rejected as cheaply as possible.
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting().AllowAnonymous();
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).DisableRateLimiting().AllowAnonymous();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
        }

        return app;
    }
}
