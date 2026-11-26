using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Onyx.Web.Localization;

namespace Onyx.Web.Customers;

/// <summary>Signs the customer out and sends them to the sign-in page (back to this page afterwards) when the API ended the session.</summary>
public sealed class CustomerSessionEndedFilter(LocalizedRouteTable routes) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Exception is not CustomerSessionEndedException)
        {
            return;
        }

        var http = context.HttpContext;
        await http.SignOutAsync(CustomerWebAuth.Scheme).ConfigureAwait(false);
        context.Result = new LocalRedirectResult(CustomerPages.SignInUrl(routes, CustomerPages.LanguageOf(http.Request.Path), CustomerPages.CurrentPath(http.Request)));
        context.ExceptionHandled = true;
    }
}
