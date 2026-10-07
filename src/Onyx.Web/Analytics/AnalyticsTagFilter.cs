using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Onyx.Web.Configuration;
using Onyx.Web.Consent;
using Onyx.Web.Security;

namespace Onyx.Web.Analytics;

/// <summary>
/// Decides, per response, whether the GA4 tag may load (architecture.md §14–15): a public page view (never admin, never a
/// [NoAnalytics] page), GA4 configured, and analytics consent given under the current policy version. Only then are Google's
/// origins added to that response's CSP (<see cref="SecurityHeadersMiddleware"/>) and the tag rendered (<c>_AnalyticsTag</c>).
/// </summary>
public sealed class AnalyticsTagFilter(IOptions<AnalyticsOptions> options, ConsentState consent) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Result is ViewResult && IsEligible(context.HttpContext))
        {
            context.HttpContext.Items[SecurityHeadersMiddleware.AnalyticsItemKey] = true;
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private bool IsEligible(HttpContext http) =>
        options.Value.IsConfigured
        && consent.AnalyticsAllowed
        && !http.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
        && http.GetEndpoint()?.Metadata.GetMetadata<NoAnalyticsAttribute>() is null;
}
