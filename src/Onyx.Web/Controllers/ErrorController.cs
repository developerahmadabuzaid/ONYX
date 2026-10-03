using Microsoft.AspNetCore.Mvc;
using Onyx.Web.Localization;

namespace Onyx.Web.Controllers;

/// <summary>Generic error pages. Never shows exception details.</summary>
public sealed class ErrorController(SeoContext seo) : Controller
{
    [Route("/error/{statusCode:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult Index(int statusCode)
    {
        seo.NoIndex = true;
        Response.StatusCode = statusCode is >= 400 and < 600 ? statusCode : 500;
        return View("Error", statusCode);
    }
}
