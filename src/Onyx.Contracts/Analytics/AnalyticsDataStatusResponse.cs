namespace Onyx.Contracts.Analytics;

/// <summary>
/// The state of one data source for a report. <see cref="State"/>: <c>NotConfigured</c>, <c>Disabled</c>, <c>Failed</c>
/// (last good data may still be shown, see <see cref="DataThroughDate"/>), <c>NoData</c> (connected, nothing for the period) or
/// <c>Ok</c>. First-party Onyx data uses provider key <c>onyx</c> and is always <c>Ok</c> or <c>NoData</c>.
/// </summary>
public sealed record AnalyticsDataStatusResponse(
    string ProviderKey,
    string State,
    bool IsStale,
    bool IsSyncing,
    DateTimeOffset? LastSyncAtUtc,
    DateTimeOffset? LastFailureAtUtc,
    string? LastFailureCode,
    DateOnly? DataFromDate,
    DateOnly? DataThroughDate,
    bool LastRunTruncated);
