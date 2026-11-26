namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>A named series of values (null = no value at that point). Dashed marks a reference line such as the ideal burndown.</summary>
public sealed record ChartSeries(string Label, string Tone, IReadOnlyList<double?> Values, bool Dashed = false, bool Area = false);
