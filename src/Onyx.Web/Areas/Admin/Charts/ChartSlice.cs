namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>One part of a donut. <see cref="Tone"/> picks the token colour (teal, sky, green, amber, violet, rose, coral, slate).</summary>
public sealed record ChartSlice(string Label, double Value, string Tone);
