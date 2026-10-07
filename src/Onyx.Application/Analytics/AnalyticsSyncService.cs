using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Content;
using Onyx.Application.Intelligence;
using Onyx.Application.Providers;
using Onyx.Domain.Analytics;
using Onyx.Domain.Common;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Providers;

namespace Onyx.Application.Analytics;

/// <summary>
/// Copies daily aggregates from the analytics providers into local tables (architecture.md §15: sync, not live calls).
/// <list type="bullet">
/// <item>Not configured or disabled → skipped (never failed, never fake zeroes).</item>
/// <item>Bounded window: the first run loads <see cref="AnalyticsSettings.InitialDays"/>, later runs re-load the last
/// <see cref="AnalyticsSettings.RefreshDays"/> (providers revise recent days), never more than <see cref="AnalyticsSettings.MaxDaysPerRun"/>,
/// and never days whose data is not final yet.</item>
/// <item>Idempotent: the synced days are replaced in one transaction, so retries never duplicate rows.</item>
/// <item>Bounded cardinality: per day and report only the largest rows are kept; the rest are summed into "(other)".</item>
/// <item>Weekly <see cref="ContentPerformanceSnapshot"/>s per CMS page and language feed the content-intelligence layer.</item>
/// </list>
/// </summary>
public sealed class AnalyticsSyncService(
    IWebAnalyticsProvider web,
    ISearchPerformanceProvider search,
    IAnalyticsMetricRepository metrics,
    IAnalyticsSyncStateRepository states,
    IProviderConnectionRepository connections,
    IProviderConnectionService providers,
    IContentRepository content,
    IContentPathRepository paths,
    IContentPerformanceRepository performance,
    IUnitOfWork unitOfWork,
    AnalyticsSettings settings,
    TimeProvider timeProvider)
{
    public const int SnapshotTake = 1_000;

    public async Task<AnalyticsSyncOutcome> SyncWebAnalyticsAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync(web.ProviderKey, web.IsConfigured, ProviderCategory.Analytics, cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        var (state, from, through) = await StartAsync(web.ProviderKey, settings.WebAnalyticsDelayDays, cancellationToken).ConfigureAwait(false);
        var fetched = await FetchAsync(token => web.FetchAsync(Request(from, through), token), cancellationToken).ConfigureAwait(false);
        if (fetched.IsFailure)
        {
            return await FailAsync(state, web.ProviderKey, ProviderCategory.Analytics, fetched.Error!.Code, cancellationToken).ConfigureAwait(false);
        }

        var now = timeProvider.GetUtcNow();
        var map = await ContentMapAsync(cancellationToken).ConfigureAwait(false);
        var batch = fetched.Value;
        var rows = new List<WebAnalyticsDailyMetric>();
        rows.AddRange(batch.Totals.Where(row => InRange(row.Date, from, through)).GroupBy(row => row.Date).Select(day =>
            WebAnalyticsDailyMetric.DailyTotal(web.ProviderKey, day.Key, day.Sum(row => row.ActiveUsers), day.Sum(row => row.Sessions), day.Sum(row => row.EngagedSessions),
                day.Sum(row => row.PageViews), now)));
        var truncated = batch.Truncated;
        foreach (var day in batch.Acquisition.Where(row => InRange(row.Date, from, through)).GroupBy(row => row.Date))
        {
            var merged = day
                .Select(row => (Row: row, Page: AnalyticsPaths.Normalize(row.LandingPage)))
                .GroupBy(item => (item.Page.Path, Source: Clean(item.Row.Source, AnalyticsDimensions.ShortMaxLength), Medium: Clean(item.Row.Medium, AnalyticsDimensions.ShortMaxLength),
                    Campaign: Clean(item.Row.Campaign, AnalyticsDimensions.CampaignMaxLength), Country: Clean(item.Row.Country, AnalyticsDimensions.ShortMaxLength),
                    Device: Clean(item.Row.DeviceCategory, WebAnalyticsDailyMetric.DeviceMaxLength)))
                .Select(group => (Key: group.Key, Language: group.First().Page.LanguageCode, Sessions: group.Sum(item => item.Row.Sessions),
                    Engaged: group.Sum(item => item.Row.EngagedSessions), Views: group.Sum(item => item.Row.PageViews)))
                .OrderByDescending(item => item.Sessions).ThenBy(item => item.Key.Path, StringComparer.Ordinal)
                .ToList();
            var kept = merged.Take(settings.MaxRowsPerDay - 1).ToList();
            rows.AddRange(kept.Select(item => WebAnalyticsDailyMetric.Acquisition(
                web.ProviderKey, day.Key, item.Key.Path.Length == 0 ? AnalyticsDimensions.NotSet : item.Key.Path, item.Language, item.Key.Source, item.Key.Medium,
                item.Key.Campaign, item.Key.Country, item.Key.Device, ContentId(map, item.Key.Path), item.Sessions, item.Engaged, item.Views, now)));
            if (merged.Count > kept.Count)
            {
                var rest = merged.Skip(kept.Count).ToList();
                truncated = true;
                rows.Add(WebAnalyticsDailyMetric.Acquisition(web.ProviderKey, day.Key, AnalyticsDimensions.Other, string.Empty, AnalyticsDimensions.Other,
                    AnalyticsDimensions.Other, AnalyticsDimensions.Other, AnalyticsDimensions.Other, AnalyticsDimensions.Other, null, rest.Sum(item => item.Sessions),
                    rest.Sum(item => item.Engaged), rest.Sum(item => item.Views), now));
            }
        }

        rows.AddRange(batch.Events
            .Where(row => InRange(row.Date, from, through) && ConversionEvents.All.Contains(row.EventName, StringComparer.Ordinal))
            .GroupBy(row => (row.Date, row.EventName))
            .Select(group => WebAnalyticsDailyMetric.ConversionEvent(web.ProviderKey, group.Key.Date, group.Key.EventName, group.Sum(row => row.EventCount), now)));

        await CommitAsync(state, from, through, rows.Count, truncated, token => metrics.ReplaceWebAsync(web.ProviderKey, from, through, rows, token), cancellationToken)
            .ConfigureAwait(false);
        await SnapshotWeeksAsync(EvidenceSource.WebAnalytics, web.ProviderKey, from, through, cancellationToken).ConfigureAwait(false);
        await providers.ReportSuccessAsync(web.ProviderKey, ProviderCategory.Analytics, cancellationToken).ConfigureAwait(false);
        return AnalyticsSyncOutcome.Synced(from, through, rows.Count, truncated);
    }

    public async Task<AnalyticsSyncOutcome> SyncSearchPerformanceAsync(CancellationToken cancellationToken)
    {
        var gate = await GateAsync(search.ProviderKey, search.IsConfigured, ProviderCategory.SearchConsole, cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        var (state, from, through) = await StartAsync(search.ProviderKey, settings.SearchDelayDays, cancellationToken).ConfigureAwait(false);
        var fetched = await FetchAsync(token => search.FetchAsync(Request(from, through), token), cancellationToken).ConfigureAwait(false);
        if (fetched.IsFailure)
        {
            return await FailAsync(state, search.ProviderKey, ProviderCategory.SearchConsole, fetched.Error!.Code, cancellationToken).ConfigureAwait(false);
        }

        var now = timeProvider.GetUtcNow();
        var map = await ContentMapAsync(cancellationToken).ConfigureAwait(false);
        var batch = fetched.Value;
        var truncated = batch.Truncated;
        var rows = new List<SearchPerformanceDailyMetric>();
        rows.AddRange(Fold(batch.Totals, SearchPerformanceReportKind.DailyTotal, _ => (string.Empty, string.Empty, string.Empty, string.Empty), from, through, now, map, ref truncated));
        rows.AddRange(Fold(batch.Pages, SearchPerformanceReportKind.Page, row => (row.Page ?? string.Empty, string.Empty, string.Empty, string.Empty), from, through, now, map, ref truncated));
        rows.AddRange(Fold(batch.Queries, SearchPerformanceReportKind.Query, row => (string.Empty, row.Query ?? string.Empty, string.Empty, string.Empty), from, through, now, map, ref truncated));
        rows.AddRange(Fold(batch.CountryDevices, SearchPerformanceReportKind.CountryDevice, row => (string.Empty, string.Empty, row.Country ?? string.Empty, row.Device ?? string.Empty),
            from, through, now, map, ref truncated));

        await CommitAsync(state, from, through, rows.Count, truncated, token => metrics.ReplaceSearchAsync(search.ProviderKey, from, through, rows, token), cancellationToken)
            .ConfigureAwait(false);
        await SnapshotWeeksAsync(EvidenceSource.SearchConsole, search.ProviderKey, from, through, cancellationToken).ConfigureAwait(false);
        await providers.ReportSuccessAsync(search.ProviderKey, ProviderCategory.SearchConsole, cancellationToken).ConfigureAwait(false);
        return AnalyticsSyncOutcome.Synced(from, through, rows.Count, truncated);
    }

    /// <summary>
    /// The days to request: from the day after the last synced day (minus the refresh window), or the initial window; through
    /// the last final day; at most <see cref="AnalyticsSettings.MaxDaysPerRun"/> days (a long gap catches up over several runs).
    /// </summary>
    public static (DateOnly From, DateOnly Through) Window(DateOnly today, int delayDays, DateOnly? lastSynced, AnalyticsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var latestFinal = today.AddDays(-Math.Max(delayDays, 1));
        var from = lastSynced is { } last ? last.AddDays(1 - Math.Max(settings.RefreshDays, 1)) : latestFinal.AddDays(1 - Math.Max(settings.InitialDays, 1));
        if (from > latestFinal)
        {
            from = latestFinal;
        }

        var through = from.AddDays(Math.Max(settings.MaxDaysPerRun, 1) - 1);
        return (from, through < latestFinal ? through : latestFinal);
    }

    private static string Clean(string? value, int maxLength) => AnalyticsDimensions.Clean(value, maxLength);

    /// <summary>The CMS page at this site path, or null (never an empty id) when the path is not a current public page.</summary>
    private static Guid? ContentId(IReadOnlyDictionary<string, Guid> map, string path) => map.TryGetValue(path, out var id) ? id : null;

    private static bool InRange(DateOnly date, DateOnly from, DateOnly through) => date >= from && date <= through;

    private AnalyticsFetchRequest Request(DateOnly from, DateOnly through) => new(from, through, settings.MaxRowsPerReport, ConversionEvents.All);

    private List<SearchPerformanceDailyMetric> Fold(
        IReadOnlyList<SearchPerformanceRow> source, SearchPerformanceReportKind kind, Func<SearchPerformanceRow, (string Page, string Query, string Country, string Device)> key,
        DateOnly from, DateOnly through, DateTimeOffset now, IReadOnlyDictionary<string, Guid> map, ref bool truncated)
    {
        var result = new List<SearchPerformanceDailyMetric>();
        foreach (var day in source.Where(row => InRange(row.Date, from, through)).GroupBy(row => row.Date))
        {
            var merged = day
                .Select(row => (Row: row, Key: key(row)))
                .Select(item => (item.Row, Page: kind == SearchPerformanceReportKind.Page ? AnalyticsPaths.Normalize(item.Key.Page) : (string.Empty, string.Empty),
                    Query: kind == SearchPerformanceReportKind.Query ? Clean(item.Key.Query, AnalyticsDimensions.QueryMaxLength) : string.Empty,
                    Country: kind == SearchPerformanceReportKind.CountryDevice ? Clean(item.Key.Country, AnalyticsDimensions.ShortMaxLength) : string.Empty,
                    Device: kind == SearchPerformanceReportKind.CountryDevice ? Clean(item.Key.Device, SearchPerformanceDailyMetric.DeviceMaxLength) : string.Empty))
                .GroupBy(item => (item.Page.Item1, item.Query, item.Country, item.Device))
                .Select(group => (Key: group.Key, Language: group.First().Page.Item2, Clicks: group.Sum(item => item.Row.Clicks), Impressions: group.Sum(item => item.Row.Impressions),
                    Position: WeightedPosition(group.Select(item => (item.Row.Position, item.Row.Impressions)))))
                .OrderByDescending(item => item.Impressions).ThenBy(item => item.Key.Item1 + item.Key.Query, StringComparer.Ordinal)
                .ToList();
            var cap = kind == SearchPerformanceReportKind.DailyTotal ? 1 : settings.MaxRowsPerDay - 1;
            var kept = merged.Take(cap).ToList();
            result.AddRange(kept.Select(item => SearchPerformanceDailyMetric.Create(
                search.ProviderKey, day.Key, kind, item.Key.Item1.Length == 0 ? AnalyticsDimensions.NotSet : item.Key.Item1, item.Language, item.Key.Query, item.Key.Country,
                item.Key.Device, kind == SearchPerformanceReportKind.Page ? ContentId(map, item.Key.Item1) : null, item.Clicks, item.Impressions, item.Position, now)));
            if (kind != SearchPerformanceReportKind.DailyTotal && merged.Count > kept.Count)
            {
                var rest = merged.Skip(kept.Count).ToList();
                truncated = true;
                result.Add(SearchPerformanceDailyMetric.Create(
                    search.ProviderKey, day.Key, kind, AnalyticsDimensions.Other, string.Empty, AnalyticsDimensions.Other, AnalyticsDimensions.Other, AnalyticsDimensions.Other,
                    null, rest.Sum(item => item.Clicks), rest.Sum(item => item.Impressions), WeightedPosition(rest.Select(item => (item.Position, item.Impressions))), now));
            }
        }

        return result;
    }

    private static double WeightedPosition(IEnumerable<(double Position, long Impressions)> values)
    {
        var list = values.Where(value => value.Impressions > 0 && value.Position > 0).ToList();
        var impressions = list.Sum(value => value.Impressions);
        return impressions == 0 ? 0 : list.Sum(value => value.Position * value.Impressions) / impressions;
    }

    /// <summary>Null when the sync may run; otherwise the skip outcome (recording "not configured" on the connection).</summary>
    private async Task<AnalyticsSyncOutcome?> GateAsync(string providerKey, bool isConfigured, ProviderCategory category, CancellationToken cancellationToken)
    {
        var connection = await connections.GetByKeyAsync(providerKey, cancellationToken).ConfigureAwait(false);
        if (connection?.Status == ProviderConnectionStatus.Disabled)
        {
            return AnalyticsSyncOutcome.Skipped("The provider is disabled.");
        }

        if (!isConfigured)
        {
            await providers.ReportNotConfiguredAsync(providerKey, category, cancellationToken).ConfigureAwait(false);
            return AnalyticsSyncOutcome.Skipped("The provider is not configured.");
        }

        return null;
    }

    private async Task<(AnalyticsSyncState State, DateOnly From, DateOnly Through)> StartAsync(string providerKey, int delayDays, CancellationToken cancellationToken)
    {
        var state = await states.GetAsync(providerKey, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            state = AnalyticsSyncState.For(providerKey);
            states.Add(state);
        }

        var now = timeProvider.GetUtcNow();
        var (from, through) = Window(DateOnly.FromDateTime(now.UtcDateTime), delayDays, state.DataThroughDate, settings);
        state.Start(now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (state, from, through);
    }

    private async Task<Result<T>> FetchAsync<T>(Func<CancellationToken, Task<Result<T>>> fetch, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.FetchTimeout);
        try
        {
            return await fetch(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unavailable(AnalyticsProviderErrors.Timeout, "The provider did not answer in time.");
        }
    }

    private async Task<AnalyticsSyncOutcome> FailAsync(AnalyticsSyncState state, string providerKey, ProviderCategory category, string code, CancellationToken cancellationToken)
    {
        state.Fail(timeProvider.GetUtcNow(), code);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await providers.ReportFailureAsync(providerKey, category, code, cancellationToken).ConfigureAwait(false);
        return AnalyticsSyncOutcome.Failed(code);
    }

    private async Task CommitAsync(
        AnalyticsSyncState state, DateOnly from, DateOnly through, int rows, bool truncated, Func<CancellationToken, Task> replace, CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync<bool>(async token =>
        {
            await replace(token).ConfigureAwait(false);
            state.Succeed(timeProvider.GetUtcNow(), from, through, rows, truncated);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Current public paths of live CMS pages (site paths, unencoded) → content item.</summary>
    private async Task<IReadOnlyDictionary<string, Guid>> ContentMapAsync(CancellationToken cancellationToken)
    {
        var live = await content.ListLiveAsync(cancellationToken).ConfigureAwait(false);
        var current = await paths.GetCurrentPathsAsync(live.Where(item => item.HasPage).Select(item => item.Id).ToList(), cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var ((id, language), path) in current)
        {
            map[AnalyticsPaths.Normalize($"/{language}/{path}").Path] = id;
        }

        return map;
    }

    /// <summary>Weekly (Monday–Sunday) snapshots for every complete week touched by the synced days, replaced idempotently.</summary>
    private async Task SnapshotWeeksAsync(EvidenceSource source, string providerKey, DateOnly from, DateOnly through, CancellationToken cancellationToken)
    {
        var weekStart = from.AddDays(-(((int)from.DayOfWeek + 6) % 7));
        for (; weekStart.AddDays(6) <= through; weekStart = weekStart.AddDays(7))
        {
            var weekEnd = weekStart.AddDays(6);
            var now = timeProvider.GetUtcNow();
            var start = new DateTimeOffset(weekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = start.AddDays(7);
            var snapshots = new List<ContentPerformanceSnapshot>();
            if (source == EvidenceSource.SearchConsole)
            {
                foreach (var row in (await metrics.SearchBreakdownAsync(providerKey, weekStart, weekEnd, SearchDimension.Page, SnapshotTake, cancellationToken).ConfigureAwait(false))
                    .Where(row => row.ContentItemId is not null && row.LanguageCode.Length > 0))
                {
                    var snapshot = ContentPerformanceSnapshot.Collect(row.ContentItemId!.Value, row.LanguageCode, source, start, end, new Dictionary<string, double>
                    {
                        [IntelligenceStatusService.ClicksMetric] = row.Clicks,
                        ["impressions"] = row.Impressions,
                        ["ctr"] = Math.Round(row.Ctr, 4),
                        ["position"] = Math.Round(row.AveragePosition, 2),
                    }, now);
                    if (snapshot.IsSuccess)
                    {
                        snapshots.Add(snapshot.Value);
                    }
                }
            }
            else
            {
                foreach (var row in await metrics.WebContentAsync(providerKey, weekStart, weekEnd, SnapshotTake, cancellationToken).ConfigureAwait(false))
                {
                    var snapshot = ContentPerformanceSnapshot.Collect(row.ContentItemId, row.LanguageCode, source, start, end, new Dictionary<string, double>
                    {
                        ["sessions"] = row.Sessions,
                        ["engagedSessions"] = row.EngagedSessions,
                        ["pageViews"] = row.PageViews,
                    }, now);
                    if (snapshot.IsSuccess)
                    {
                        snapshots.Add(snapshot.Value);
                    }
                }
            }

            await performance.ReplacePeriodAsync(source, start, snapshots, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

/// <summary>What one sync run did (no provider data).</summary>
public sealed record AnalyticsSyncOutcome(string Status, string Message, string? FailureCode)
{
    public static AnalyticsSyncOutcome Skipped(string reason) => new("Skipped", reason, null);

    public static AnalyticsSyncOutcome Failed(string code) => new("Failed", $"The provider sync failed ({code}).", code);

    public static AnalyticsSyncOutcome Synced(DateOnly from, DateOnly through, int rows, bool truncated) =>
        new("Succeeded", string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"Synced {from:yyyy-MM-dd}–{through:yyyy-MM-dd}: {rows} aggregate rows{(truncated ? " (row cap reached; smaller rows summed as \"(other)\")" : string.Empty)}."), null);
}
