namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>A milestone on a timeline (a project's start or target date, a sprint end).</summary>
public sealed record GanttMarker(DateOnly Date, string Label, string Tone);
