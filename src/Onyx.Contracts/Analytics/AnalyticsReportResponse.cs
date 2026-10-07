namespace Onyx.Contracts.Analytics;

/// <summary>
/// One analytics report: its period, the state of every source it uses, and sections of rows. A section always names its
/// source; values are null where a metric does not exist for the row (never a stand-in zero). <see cref="Notes"/> are keys of
/// explanations shown with the report (for example "separate datasets, not joined per visitor").
/// </summary>
public sealed record AnalyticsReportResponse(
    string Report,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AnalyticsDataStatusResponse> Sources,
    IReadOnlyList<AnalyticsSectionResponse> Sections,
    IReadOnlyList<string> Notes);

/// <param name="Key">Stable section key (also its title key in the UI).</param>
/// <param name="Source">Provider key the rows come from (<c>analytics.ga4</c>, <c>search.gsc</c>) or <c>onyx</c> for first-party data.</param>
/// <param name="Columns">Metric keys, one per value.</param>
public sealed record AnalyticsSectionResponse(string Key, string Source, IReadOnlyList<string> Columns, IReadOnlyList<AnalyticsRowResponse> Rows);

/// <param name="Label">Dimension value as stored (plain text; encode when rendering).</param>
/// <param name="LanguageCode">"ar"/"en" for page rows; null otherwise.</param>
public sealed record AnalyticsRowResponse(string Label, string? LanguageCode, IReadOnlyList<double?> Values);
