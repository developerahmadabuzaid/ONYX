using Onyx.Contracts.Analytics;

namespace Onyx.Web.ApiClients;

public interface IAdminAnalyticsApiClient
{
    /// <summary><paramref name="report"/> is an API path below <c>api/v1/admin/</c>, e.g. <c>analytics/overview</c> (validated by the caller).</summary>
    Task<ApiResult<AnalyticsReportResponse>> GetReportAsync(string report, AnalyticsReportQuery query, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<AnalyticsIntegrationResponse>>> ListIntegrationsAsync(CancellationToken cancellationToken);

    Task<ApiResult<NoContent>> RequestSyncAsync(string providerKey, CancellationToken cancellationToken);
}
