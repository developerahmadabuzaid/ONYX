using Microsoft.EntityFrameworkCore;
using Onyx.Application.Analytics;
using Onyx.Domain.Analytics;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>Local analytics aggregates. Reads group and sum in PostgreSQL, filtered by provider, kind and day range, capped.</summary>
internal sealed class AnalyticsMetricRepository(ApplicationDbContext dbContext) : IAnalyticsMetricRepository
{
    public async Task ReplaceWebAsync(string providerKey, DateOnly from, DateOnly to, IReadOnlyList<WebAnalyticsDailyMetric> rows, CancellationToken cancellationToken)
    {
        await dbContext.WebAnalyticsDailyMetrics.Where(metric => metric.ProviderKey == providerKey && metric.Date >= from && metric.Date <= to)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        dbContext.WebAnalyticsDailyMetrics.AddRange(rows);
    }

    public async Task ReplaceSearchAsync(string providerKey, DateOnly from, DateOnly to, IReadOnlyList<SearchPerformanceDailyMetric> rows, CancellationToken cancellationToken)
    {
        await dbContext.SearchPerformanceDailyMetrics.Where(metric => metric.ProviderKey == providerKey && metric.Date >= from && metric.Date <= to)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        dbContext.SearchPerformanceDailyMetrics.AddRange(rows);
    }

    public async Task<IReadOnlyList<WebDailyTotal>> WebTotalsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Web(providerKey, WebAnalyticsReportKind.DailyTotal, from, to)
            .OrderBy(metric => metric.Date)
            .Select(metric => new WebDailyTotal(metric.Date, metric.ActiveUsers ?? 0, metric.Sessions, metric.EngagedSessions, metric.PageViews))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<WebBreakdownRow>> WebBreakdownAsync(
        string providerKey, DateOnly from, DateOnly to, WebDimension dimension, int take, CancellationToken cancellationToken)
    {
        var rows = Web(providerKey, WebAnalyticsReportKind.Acquisition, from, to);
        var grouped = dimension switch
        {
            WebDimension.SourceMedium => rows.GroupBy(metric => new { Key = metric.Source + " / " + metric.Medium, Language = string.Empty, ContentItemId = (Guid?)null }),
            WebDimension.Campaign => rows.GroupBy(metric => new { Key = metric.Campaign, Language = string.Empty, ContentItemId = (Guid?)null }),
            WebDimension.LandingPage => rows.GroupBy(metric => new { Key = metric.LandingPage, Language = metric.LanguageCode, metric.ContentItemId }),
            WebDimension.Country => rows.GroupBy(metric => new { Key = metric.Country, Language = string.Empty, ContentItemId = (Guid?)null }),
            _ => rows.GroupBy(metric => new { Key = metric.DeviceCategory, Language = string.Empty, ContentItemId = (Guid?)null }),
        };
        var sums = await grouped
            .Select(group => new
            {
                group.Key.Key,
                group.Key.Language,
                group.Key.ContentItemId,
                Sessions = group.Sum(metric => metric.Sessions),
                Engaged = group.Sum(metric => metric.EngagedSessions),
                Views = group.Sum(metric => metric.PageViews),
            })
            .OrderByDescending(row => row.Sessions).ThenBy(row => row.Key)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return sums.Select(row => new WebBreakdownRow(row.Key, row.Language, row.ContentItemId, row.Sessions, row.Engaged, row.Views)).ToList();
    }

    public async Task<IReadOnlyList<WebEventTotal>> WebEventsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Web(providerKey, WebAnalyticsReportKind.ConversionEvent, from, to)
            .GroupBy(metric => metric.EventName)
            .Select(group => new WebEventTotal(group.Key, group.Sum(metric => metric.EventCount)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<ContentTrafficRow>> WebContentAsync(string providerKey, DateOnly from, DateOnly to, int take, CancellationToken cancellationToken)
    {
        var sums = await Web(providerKey, WebAnalyticsReportKind.Acquisition, from, to)
            .Where(metric => metric.ContentItemId != null && metric.LanguageCode != string.Empty)
            .GroupBy(metric => new { metric.ContentItemId, metric.LanguageCode })
            .Select(group => new
            {
                group.Key.ContentItemId,
                group.Key.LanguageCode,
                Sessions = group.Sum(metric => metric.Sessions),
                Engaged = group.Sum(metric => metric.EngagedSessions),
                Views = group.Sum(metric => metric.PageViews),
            })
            .OrderByDescending(row => row.Sessions)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return sums.Select(row => new ContentTrafficRow(row.ContentItemId!.Value, row.LanguageCode, row.Sessions, row.Engaged, row.Views)).ToList();
    }

    public async Task<IReadOnlyList<SearchDailyTotal>> SearchTotalsAsync(string providerKey, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await Search(providerKey, SearchPerformanceReportKind.DailyTotal, from, to)
            .OrderBy(metric => metric.Date)
            .Select(metric => new SearchDailyTotal(metric.Date, metric.Clicks, metric.Impressions, metric.AveragePosition))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SearchBreakdownRow>> SearchBreakdownAsync(
        string providerKey, DateOnly from, DateOnly to, SearchDimension dimension, int take, CancellationToken cancellationToken)
    {
        var kind = dimension switch
        {
            SearchDimension.Page => SearchPerformanceReportKind.Page,
            SearchDimension.Query => SearchPerformanceReportKind.Query,
            _ => SearchPerformanceReportKind.CountryDevice,
        };
        var rows = Search(providerKey, kind, from, to);
        var grouped = dimension switch
        {
            SearchDimension.Page => rows.GroupBy(metric => new { Key = metric.Page, Language = metric.LanguageCode, metric.ContentItemId }),
            SearchDimension.Query => rows.GroupBy(metric => new { Key = metric.Query, Language = string.Empty, ContentItemId = (Guid?)null }),
            SearchDimension.Country => rows.GroupBy(metric => new { Key = metric.Country, Language = string.Empty, ContentItemId = (Guid?)null }),
            _ => rows.GroupBy(metric => new { Key = metric.Device, Language = string.Empty, ContentItemId = (Guid?)null }),
        };
        var sums = await grouped
            .Select(group => new
            {
                group.Key.Key,
                group.Key.Language,
                group.Key.ContentItemId,
                Clicks = group.Sum(metric => metric.Clicks),
                Impressions = group.Sum(metric => metric.Impressions),
                WeightedPosition = group.Sum(metric => metric.AveragePosition * metric.Impressions),
            })
            .OrderByDescending(row => row.Impressions).ThenBy(row => row.Key)
            .Take(take)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return sums.Select(row => new SearchBreakdownRow(row.Key, row.Language, row.ContentItemId, row.Clicks, row.Impressions,
            row.Impressions == 0 ? 0 : row.WeightedPosition / row.Impressions)).ToList();
    }

    private IQueryable<WebAnalyticsDailyMetric> Web(string providerKey, WebAnalyticsReportKind kind, DateOnly from, DateOnly to) =>
        dbContext.WebAnalyticsDailyMetrics.AsNoTracking().Where(metric => metric.ProviderKey == providerKey && metric.Kind == kind && metric.Date >= from && metric.Date <= to);

    private IQueryable<SearchPerformanceDailyMetric> Search(string providerKey, SearchPerformanceReportKind kind, DateOnly from, DateOnly to) =>
        dbContext.SearchPerformanceDailyMetrics.AsNoTracking().Where(metric => metric.ProviderKey == providerKey && metric.Kind == kind && metric.Date >= from && metric.Date <= to);
}
