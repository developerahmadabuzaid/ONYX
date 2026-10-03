using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Onyx.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[Route("admin")]
[TypeFilter<AdminShellFilter>]
public sealed class DashboardController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
