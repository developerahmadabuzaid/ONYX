namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>A shaded period on a timeline, such as a sprint.</summary>
public sealed record GanttBand(DateOnly Start, DateOnly End, string Label);
