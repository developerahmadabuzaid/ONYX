using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Onyx.Web.Authentication;

/// <summary>Signs the admin out and sends them to the login page when the API ends their session mid-request.</summary>
public sealed class AdminSessionEndedFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Exception is not AdminSessionEndedException)
        {
            return;
        }

        await context.HttpContext.SignOutAsync(BffDefaults.Scheme).ConfigureAwait(false);
        var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
        context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin", returnUrl });
        context.ExceptionHandled = true;
    }
}
