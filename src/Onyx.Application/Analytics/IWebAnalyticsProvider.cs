using Onyx.Domain.Common;

namespace Onyx.Application.Analytics;

/// <summary>
/// Provider-neutral web analytics (architecture.md §15; GA4 today, in Infrastructure). Returns daily aggregates only: no
/// visitor-, client- or session-level identifiers. Inactive until configured: <see cref="IsConfigured"/> is false without the
/// owner's credentials and property, and the sync job then skips. Failures are expected results with a safe code
/// (<see cref="AnalyticsProviderErrors"/>); provider messages and credentials never leave the adapter.
/// </summary>
public interface IWebAnalyticsProvider
{
    string ProviderKey { get; }

    bool IsConfigured { get; }

    Task<Result<WebAnalyticsBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken);
}

/// <param name="ConversionEvents">Only these event names are requested (Onyx's own conversion events).</param>
/// <param name="MaxRowsPerReport">Upper bound on rows read per report (the adapter pages up to it).</param>
public sealed record AnalyticsFetchRequest(DateOnly From, DateOnly To, int MaxRowsPerReport, IReadOnlyList<string> ConversionEvents);

/// <param name="Truncated">A report had more rows than <see cref="AnalyticsFetchRequest.MaxRowsPerReport"/>.</param>
public sealed record WebAnalyticsBatch(
    IReadOnlyList<WebAnalyticsTotalRow> Totals,
    IReadOnlyList<WebAnalyticsAcquisitionRow> Acquisition,
    IReadOnlyList<WebAnalyticsEventRow> Events,
    bool Truncated);

public sealed record WebAnalyticsTotalRow(DateOnly Date, long ActiveUsers, long Sessions, long EngagedSessions, long PageViews);

/// <param name="LandingPage">The provider's landing page path (may include a query string, which Onyx drops).</param>
public sealed record WebAnalyticsAcquisitionRow(
    DateOnly Date, string? LandingPage, string? Source, string? Medium, string? Campaign, string? Country, string? DeviceCategory,
    long Sessions, long EngagedSessions, long PageViews);

public sealed record WebAnalyticsEventRow(DateOnly Date, string EventName, long EventCount);
