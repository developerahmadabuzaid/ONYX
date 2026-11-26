namespace Onyx.Web.Areas.Admin.Charts;

/// <summary>
/// One timeline row. A row with both dates is a bar; a row with one date is a milestone point. <see cref="Progress"/> is the real
/// done share (0..1) or null when there is nothing to measure.
/// </summary>
public sealed record GanttRow(
    string Label,
    string? Key,
    string? Url,
    DateOnly? Start,
    DateOnly? End,
    string Tone,
    double? Progress,
    int Depth,
    string Tip,
    string? Meta = null);
