using System.Globalization;
using Onyx.Contracts.Analytics;

namespace Onyx.Web.ApiClients;

public sealed class AdminAnalyticsApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IAdminAnalyticsApiClient
{
    public Task<ApiResult<AnalyticsReportResponse>> GetReportAsync(string report, AnalyticsReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var parameters = new List<string>();
        if (query.From is { } from)
        {
            parameters.Add("from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (query.To is { } to)
        {
            parameters.Add("to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(query.GroupBy))
        {
            parameters.Add("groupBy=" + Uri.EscapeDataString(query.GroupBy));
        }

        if (!string.IsNullOrWhiteSpace(query.Touch))
        {
            parameters.Add("touch=" + Uri.EscapeDataString(query.Touch));
        }

        return GetAsync<AnalyticsReportResponse>($"api/v1/admin/{report}{(parameters.Count > 0 ? "?" + string.Join('&', parameters) : string.Empty)}", cancellationToken);
    }

    public Task<ApiResult<IReadOnlyList<AnalyticsIntegrationResponse>>> ListIntegrationsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<AnalyticsIntegrationResponse>>("api/v1/admin/analytics-integrations", cancellationToken);

    public Task<ApiResult<NoContent>> RequestSyncAsync(string providerKey, CancellationToken cancellationToken) =>
        PostAsync<NoContent>($"api/v1/admin/analytics-integrations/{Uri.EscapeDataString(providerKey)}/sync", null, cancellationToken);
}
