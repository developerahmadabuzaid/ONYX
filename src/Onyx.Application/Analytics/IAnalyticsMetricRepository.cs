using Onyx.Domain.Analytics;

namespace Onyx.Application.Analytics;

/// <summary>Local provider aggregates: replaced per synced day range (idempotent), read with bounded, grouped queries.</summary>
public interface IAnalyticsMetricRepository
{
    /// <summary>Deletes the provider's rows for <paramref name="from"/>–<paramref name="to"/> and stages <paramref name="rows"/> (caller commits).</summary>
    Task ReplaceWebAsync(string providerKey, DateOnly from, DateOnly to, IReadOnlyList<WebAnalyticsDailyMetric> rows, CancellationToken cancellationToken);

    Task ReplaceSearchAsync(string providerKey, DateOnly from, DateOnly to, IReadOnlyList<SearchPerformanceDailyMetric> rows, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebDailyTotal>> WebTotalsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebBreakdownRow>> WebBreakdownAsync(string providerKey, DateOnly from, DateOnly to, WebDimension dimension, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebEventTotal>> WebEventsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentTrafficRow>> WebContentAsync(string providerKey, DateOnly from, DateOnly to, int take, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchDailyTotal>> SearchTotalsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchBreakdownRow>> SearchBreakdownAsync(
        string providerKey, DateOnly from, DateOnly to, SearchDimension dimension, int take, CancellationToken cancellationToken);
}

public enum WebDimension
{
    SourceMedium = 0,
    Campaign = 1,
    LandingPage = 2,
    Country = 3,
    Device = 4,
}

public enum SearchDimension
{
    Page = 0,
    Query = 1,
    Country = 2,
    Device = 3,
}

public sealed record WebDailyTotal(DateOnly Date, long ActiveUsers, long Sessions, long EngagedSessions, long PageViews);

/// <param name="Key">The dimension value (for source/medium: "source / medium").</param>
public sealed record WebBreakdownRow(string Key, string LanguageCode, Guid? ContentItemId, long Sessions, long EngagedSessions, long PageViews);

public sealed record WebEventTotal(string EventName, long EventCount);

public sealed record ContentTrafficRow(Guid ContentItemId, string LanguageCode, long Sessions, long EngagedSessions, long PageViews);

public sealed record SearchDailyTotal(DateOnly Date, long Clicks, long Impressions, double AveragePosition);

/// <param name="AveragePosition">Impression-weighted average of the provider's daily positions.</param>
public sealed record SearchBreakdownRow(string Key, string LanguageCode, Guid? ContentItemId, long Clicks, long Impressions, double AveragePosition)
{
    public double Ctr => Impressions == 0 ? 0 : (double)Clicks / Impressions;
}
