using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Onyx.Web.Localization;

namespace Onyx.Web.Customers;

/// <summary>Sends signed-out customers to the sign-in page of their language, remembering where they were going.</summary>
public sealed class CustomerCookieEvents(LocalizedRouteTable routes) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var request = context.HttpContext.Request;
        context.Response.Redirect(CustomerPages.SignInUrl(routes, CustomerPages.LanguageOf(request.Path), CustomerPages.CurrentPath(request)));
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) => RedirectToLogin(context);
}
