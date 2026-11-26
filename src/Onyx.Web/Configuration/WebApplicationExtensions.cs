using Onyx.Web.Localization;
using Onyx.Web.Security;

namespace Onyx.Web.Configuration;

public static class WebApplicationExtensions
{
    public static WebApplication UseOnyxWeb(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error/500");
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/error/{0}");
        app.UseHttpsRedirection();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseStaticFiles();

        app.UseRouting();

        // Authentication before localization so the admin's preferred language is known.
        app.UseAuthentication();
        app.UseRequestLocalization();
        app.UseAuthorization();
        app.UseMiddleware<Attribution.AttributionMiddleware>();

        // Public pages read editor overrides from an in-memory snapshot; keep it fresh without blocking page views.
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
            {
                await context.RequestServices.GetRequiredService<Content.SiteContent.SiteContentProvider>().EnsureFreshAsync(context.RequestAborted);
            }

            await next(context);
        });

        app.MapControllers();
        app.MapDynamicControllerRoute<LocalizedRouteTransformer>($"{{lang:{LanguageRouteConstraint.Name}}}/{{**path}}");
        return app;
    }
}
