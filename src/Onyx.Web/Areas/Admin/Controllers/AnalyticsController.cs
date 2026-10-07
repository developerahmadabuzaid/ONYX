using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Analytics;
using Onyx.Contracts.Authorization;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;
using Onyx.Web.Authentication;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>
/// Admin analytics: web analytics, the first-party funnel, organic search and integration status. Every page reads local
/// aggregates through the API (which authorizes each report) and states its period, sources and their state. Tabs are shown
/// only for reports the user may open; the API is the authority. Never cached (<see cref="CmsControllerBase"/>).
/// </summary>
[Route("admin/analytics")]
public sealed class AnalyticsController(IAdminAnalyticsApiClient api, CurrentAdminContext admin) : CmsControllerBase
{
    /// <summary>Report key → (API path, permission). Only these paths are ever requested.</summary>
    public static IReadOnlyDictionary<string, (string Path, string Permission)> Reports { get; } = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["overview"] = ("analytics/overview", Permissions.Analytics.View),
        ["traffic"] = ("analytics/traffic", Permissions.Analytics.View),
        ["landing-pages"] = ("analytics/landing-pages", Permissions.Analytics.View),
        ["content"] = ("analytics/content", Permissions.Analytics.View),
        ["funnel"] = ("analytics/funnel", Permissions.Analytics.View),
        ["campaigns"] = ("analytics/campaigns", Permissions.Analytics.View),
        ["conversions"] = ("analytics/conversions", Permissions.Analytics.View),
        ["search"] = ("search-performance/overview", Permissions.SearchPerformance.View),
        ["search-queries"] = ("search-performance/queries", Permissions.SearchPerformance.View),
        ["search-pages"] = ("search-performance/pages", Permissions.SearchPerformance.View),
        ["search-signals"] = ("search-performance/signals", Permissions.SearchPerformance.View),
    };

    [HttpGet("")]
    public Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken cancellationToken) => Report("overview", from, to, null, null, cancellationToken);

    [HttpGet("search")]
    public Task<IActionResult> Search(DateOnly? from, DateOnly? to, CancellationToken cancellationToken) => Report("search", from, to, null, null, cancellationToken);

    [HttpGet("integrations")]
    public async Task<IActionResult> Integrations(CancellationToken cancellationToken)
    {
        var result = await api.ListIntegrationsAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("integrations/{providerKey}/sync")]
    public async Task<IActionResult> Sync(string providerKey, CancellationToken cancellationToken) =>
        After(await api.RequestSyncAsync(providerKey, cancellationToken), RedirectToAction(nameof(Integrations)), "Analytics.SyncRequested");

    [HttpGet("{report}")]
    public async Task<IActionResult> Report(string report, DateOnly? from, DateOnly? to, string? groupBy, string? touch, CancellationToken cancellationToken)
    {
        if (!Reports.TryGetValue(report, out var definition))
        {
            return NotFound();
        }

        var result = await api.GetReportAsync(definition.Path, new AnalyticsReportQuery(from, to, Blank(groupBy), Blank(touch)), cancellationToken);
        if (result.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // An invalid period or grouping: show the API's message with the default report.
            AddProblem(result);
            groupBy = null;
            touch = null;
            result = await api.GetReportAsync(definition.Path, new AnalyticsReportQuery(), cancellationToken);
        }

        if (Failed(result) is { } failed)
        {
            return failed;
        }

        var tabs = new List<string>();
        foreach (var (key, (_, permission)) in Reports)
        {
            if (await admin.HasPermissionAsync(permission, cancellationToken))
            {
                tabs.Add(key);
            }
        }

        ViewData["ReportKey"] = report;
        ViewData["Tabs"] = tabs;
        ViewData["GroupBy"] = Blank(groupBy);
        ViewData["Touch"] = Blank(touch);
        return View("Report", result.Value);
    }
}
