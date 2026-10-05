using Microsoft.AspNetCore.Mvc.Filters;

namespace Onyx.Web.Security;

/// <summary>
/// Lets this page load the Cloudflare Turnstile challenge: the CSP gains that single origin for scripts and frames. Every other
/// page keeps the self-only policy.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowsTurnstileAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Items[SecurityHeadersMiddleware.TurnstileItemKey] = true;
    }
}
