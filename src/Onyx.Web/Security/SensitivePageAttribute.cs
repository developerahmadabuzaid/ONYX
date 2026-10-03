using Microsoft.AspNetCore.Mvc.Filters;

namespace Onyx.Web.Security;

/// <summary>
/// For pages that handle credentials or one-time tokens: no referrer is ever sent (tokens cannot leak via the Referer header,
/// even to our own assets) and nothing is cached by the browser or intermediaries.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SensitivePageAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var headers = context.HttpContext.Response.Headers;
        headers["Referrer-Policy"] = "no-referrer";
        headers.CacheControl = "no-store";
        headers.Pragma = "no-cache";
    }
}
