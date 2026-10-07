using System.Globalization;
using Google.Apis.AnalyticsData.v1beta;
using Google.Apis.AnalyticsData.v1beta.Data;
using Onyx.Application.Analytics;
using Onyx.Application.Providers;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Analytics;

/// <summary>
/// GA4 through the Google Analytics Data API (<c>runReport</c>, scope <c>analytics.readonly</c>). Three bounded reports per sync:
/// daily totals, daily acquisition (landing page, source, medium, campaign, country, device) and daily counts of Onyx's two
/// conversion events. Only aggregate dimensions are requested; GA4 exposes no client id here and none is asked for.
/// </summary>
internal sealed class GoogleAnalyticsDataProvider(GoogleServiceAccount account) : IWebAnalyticsProvider
{
    public const int PageSize = 10_000;

    public string ProviderKey => ProviderKeys.GoogleAnalytics;

    public bool IsConfigured => account.Options.Ga4Configured;

    public async Task<Result<WebAnalyticsBatch>> FetchAsync(AnalyticsFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsConfigured)
        {
            return Error.Unavailable(AnalyticsProviderErrors.NotConfigured, "Not configured.");
        }

        try
        {
            using var service = new AnalyticsDataService(account.Initializer(AnalyticsDataService.ScopeConstants.AnalyticsReadonly));
            service.HttpClient.Timeout = TimeSpan.FromSeconds(account.Options.RequestTimeoutSeconds);
            var property = $"properties/{account.Options.Ga4PropertyId}";
            var range = new DateRange { StartDate = Day(request.From), EndDate = Day(request.To) };

            var (totals, totalsCut) = await RunAsync(service, property, range, ["date"], ["activeUsers", "sessions", "engagedSessions", "screenPageViews"], null,
                request.MaxRowsPerReport, cancellationToken).ConfigureAwait(false);
            var (acquisition, acquisitionCut) = await RunAsync(service, property, range,
                ["date", "landingPage", "sessionSource", "sessionMedium", "sessionCampaignName", "country", "deviceCategory"],
                ["sessions", "engagedSessions", "screenPageViews"], null, request.MaxRowsPerReport, cancellationToken).ConfigureAwait(false);
            var eventFilter = new FilterExpression
            {
                Filter = new Filter { FieldName = "eventName", InListFilter = new InListFilter { Values = request.ConversionEvents.ToList(), CaseSensitive = true } },
            };
            var (events, eventsCut) = await RunAsync(service, property, range, ["date", "eventName"], ["eventCount"], eventFilter, request.MaxRowsPerReport, cancellationToken)
                .ConfigureAwait(false);

            return new WebAnalyticsBatch(
                totals.Select(row => new WebAnalyticsTotalRow(Date(row, 0), Long(row, 0), Long(row, 1), Long(row, 2), Long(row, 3))).ToList(),
                acquisition.Select(row => new WebAnalyticsAcquisitionRow(
                    Date(row, 0), Text(row, 1), Text(row, 2), Text(row, 3), Text(row, 4), Text(row, 5), Text(row, 6), Long(row, 0), Long(row, 1), Long(row, 2))).ToList(),
                events.Select(row => new WebAnalyticsEventRow(Date(row, 0), Text(row, 1) ?? string.Empty, Long(row, 0))).ToList(),
                totalsCut || acquisitionCut || eventsCut);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return GoogleErrors.Map(exception);
        }
    }

    /// <summary>Pages through one report up to <paramref name="maxRows"/>; true when the provider had more rows than that.</summary>
    private static async Task<(List<Row> Rows, bool Truncated)> RunAsync(
        AnalyticsDataService service, string property, DateRange range, string[] dimensions, string[] metrics, FilterExpression? filter, int maxRows,
        CancellationToken cancellationToken)
    {
        var rows = new List<Row>();
        long offset = 0;
        while (true)
        {
            var limit = Math.Min(PageSize, maxRows - rows.Count);
            if (limit <= 0)
            {
                return (rows, true);
            }

            var response = await service.Properties.RunReport(new RunReportRequest
            {
                DateRanges = [range],
                Dimensions = dimensions.Select(name => new Dimension { Name = name }).ToList(),
                Metrics = metrics.Select(name => new Metric { Name = name }).ToList(),
                DimensionFilter = filter,
                Limit = limit,
                Offset = offset,
                KeepEmptyRows = false,
            }, property).ExecuteAsync(cancellationToken).ConfigureAwait(false);

            var page = response.Rows ?? [];
            rows.AddRange(page);
            offset += page.Count;
            var total = response.RowCount ?? rows.Count;
            if (page.Count == 0 || rows.Count >= total)
            {
                return (rows, false);
            }
        }
    }

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly Date(Row row, int index) => DateOnly.ParseExact(row.DimensionValues[index].Value, "yyyyMMdd", CultureInfo.InvariantCulture);

    private static string? Text(Row row, int index) => row.DimensionValues[index].Value;

    private static long Long(Row row, int index) =>
        long.TryParse(row.MetricValues[index].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value
        : double.TryParse(row.MetricValues[index].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? (long)Math.Round(number) : 0;
}
