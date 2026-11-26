using Microsoft.AspNetCore.Html;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>
/// A headline figure on the workspace dashboard. <see cref="ChangePercent"/> is shown only when both periods have real counts;
/// <see cref="ChangeIsGood"/> decides its colour (more completed work is good, more overdue work is not, created work is neutral).
/// </summary>
public sealed record KpiCard(
    string LabelKey,
    int Value,
    string Icon,
    string Tone,
    string? Note,
    int? ChangePercent,
    bool? ChangeIsGood,
    IHtmlContent? Sparkline,
    string? Href,
    bool Attention = false);
