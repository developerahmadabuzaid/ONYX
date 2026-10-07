using System.Globalization;
using Google.Apis.SearchConsole.v1;
using Google.Apis.SearchConsole.v1.Data;
using Onyx.Application.Analytics;
using Onyx.Application.Providers;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Analytics;

/// <summary>
/// Google Search Console through <c>searchanalytics.query</c> (scope <c>webmasters.readonly</c>), web search, final data only
/// (Search Console's own "final" state, so no partial current-day numbers). Four bounded reports per sync: daily totals, daily
/// pages, daily queries, daily country × device. Values are exactly the provider's clicks, impressions, CTR and position.
/// </summary>
internal sealed class GoogleSearchConsoleProvider(GoogleServiceAccount account) : ISearchPerformanceProvider
{
    public const int PageSize = 25_000;

    public string ProviderKey => ProviderKeys.SearchConsole;

    public bool IsConfigured => account.Options.SearchConsoleConfigured;

    public async Task<Result<SearchPerformanceBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsConfigured)
        {
            return Error.Unavailable(AnalyticsProviderErrors.NotConfigured, "Not configured.");
        }

        try
        {
            using var service = new SearchConsoleService(account.Initializer(SearchConsoleService.ScopeConstants.WebmastersReadonly));
            service.HttpClient.Timeout = TimeSpan.FromSeconds(account.Options.RequestTimeoutSeconds);
            var site = account.Options.SearchConsoleSiteUrl!;
            var (totals, totalsCut) = await QueryAsync(service, site, request, ["date"], cancellationToken).ConfigureAwait(false);
            var (pages, pagesCut) = await QueryAsync(service, site, request, ["date", "page"], cancellationToken).ConfigureAwait(false);
            var (queries, queriesCut) = await QueryAsync(service, site, request, ["date", "query"], cancellationToken).ConfigureAwait(false);
            var (devices, devicesCut) = await QueryAsync(service, site, request, ["date", "country", "device"], cancellationToken).ConfigureAwait(false);

            return new SearchPerformanceBatch(
                totals.Select(row => Map(row, page: null, query: null, country: null, device: null)).ToList(),
                pages.Select(row => Map(row, page: Key(row, 1), query: null, country: null, device: null)).ToList(),
                queries.Select(row => Map(row, page: null, query: Key(row, 1), country: null, device: null)).ToList(),
                devices.Select(row => Map(row, page: null, query: null, country: Key(row, 1), device: Key(row, 2))).ToList(),
                totalsCut || pagesCut || queriesCut || devicesCut);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return GoogleErrors.Map(exception);
        }
    }

    private static async Task<(List<ApiDataRow> Rows, bool Truncated)> QueryAsync(
        SearchConsoleService service, string site, AnalyticsFetchRequest request, string[] dimensions, CancellationToken cancellationToken)
    {
        var rows = new List<ApiDataRow>();
        while (true)
        {
            var limit = Math.Min(PageSize, request.MaxRowsPerReport - rows.Count);
            if (limit <= 0)
            {
                return (rows, true);
            }

            var response = await service.Searchanalytics.Query(new SearchAnalyticsQueryRequest
            {
                StartDate = request.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDate = request.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Dimensions = dimensions.ToList(),
                RowLimit = limit,
                StartRow = rows.Count,
                Type = "web",
                DataState = "final",
            }, site).ExecuteAsync(cancellationToken).ConfigureAwait(false);

            var page = response.Rows ?? [];
            rows.AddRange(page);
            if (page.Count < limit)
            {
                return (rows, false);
            }
        }
    }

    private static SearchPerformanceRow Map(ApiDataRow row, string? page, string? query, string? country, string? device) =>
        new(DateOnly.ParseExact(Key(row, 0)!, "yyyy-MM-dd", CultureInfo.InvariantCulture), page, query, country, device,
            (long)Math.Round(row.Clicks ?? 0), (long)Math.Round(row.Impressions ?? 0), row.Ctr ?? 0, row.Position ?? 0);

    private static string? Key(ApiDataRow row, int index) => row.Keys is { } keys && keys.Count > index ? keys[index] : null;
}
