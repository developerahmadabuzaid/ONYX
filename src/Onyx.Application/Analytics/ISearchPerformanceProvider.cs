using Onyx.Domain.Common;

namespace Onyx.Application.Analytics;

/// <summary>
/// Provider-neutral search performance (architecture.md §15; Google Search Console today, in Infrastructure). Daily aggregates
/// only, exactly as the provider reports them: clicks, impressions, CTR and average position. No search volume, difficulty or
/// ranking estimate exists here. Inactive until configured.
/// </summary>
public interface ISearchPerformanceProvider
{
    string ProviderKey { get; }

    bool IsConfigured { get; }

    Task<Result<SearchPerformanceBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken);
}

public sealed record SearchPerformanceBatch(
    IReadOnlyList<SearchPerformanceRow> Totals,
    IReadOnlyList<SearchPerformanceRow> Pages,
    IReadOnlyList<SearchPerformanceRow> Queries,
    IReadOnlyList<SearchPerformanceRow> CountryDevices,
    bool Truncated);

/// <param name="Page">Absolute page URL as the provider reports it (page rows only).</param>
public sealed record SearchPerformanceRow(
    DateOnly Date, string? Page, string? Query, string? Country, string? Device, long Clicks, long Impressions, double Ctr, double Position);
