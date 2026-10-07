using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Jobs;
using Onyx.Application.Providers;
using Onyx.Contracts.Analytics;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Providers;

namespace Onyx.Application.Analytics;

/// <summary>
/// Operational view of the analytics providers for <c>ProviderConnections.Manage</c>: connection status, data coverage and a
/// "sync now" request. The request only makes the Worker job due; no provider is called from the API. Audited. Nothing here
/// reads or returns credentials, tokens or account identifiers.
/// </summary>
public sealed class AnalyticsIntegrationService(
    IProviderConnectionRepository connections,
    AnalyticsReportService reports,
    IJobTrigger jobs,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork)
{
    public const string WebAnalyticsJob = "analytics-ga4-sync";
    public const string SearchPerformanceJob = "analytics-gsc-sync";

    private static readonly IReadOnlyDictionary<string, string> JobsByProvider = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ProviderKeys.GoogleAnalytics] = WebAnalyticsJob,
        [ProviderKeys.SearchConsole] = SearchPerformanceJob,
    };

    public async Task<IReadOnlyList<AnalyticsIntegrationResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<AnalyticsIntegrationResponse>();
        foreach (var (providerKey, job) in JobsByProvider)
        {
            var connection = await connections.GetByKeyAsync(providerKey, cancellationToken).ConfigureAwait(false);
            var data = await reports.StatusAsync(providerKey, hasDataInPeriod: true, cancellationToken).ConfigureAwait(false);
            if (data.DataThroughDate is null && data.State == "Ok")
            {
                data = data with { State = "NoData" };
            }

            result.Add(new AnalyticsIntegrationResponse(
                providerKey, (connection?.Status ?? ProviderConnectionStatus.NotConfigured).ToString(), connection?.LastCheckedAtUtc, connection?.LastSucceededAtUtc,
                connection?.LastError, data, job));
        }

        return result;
    }

    /// <summary>Makes the provider's sync job due now (the Worker runs it under its lease); also serves as the connection test.</summary>
    public async Task<Result<bool>> RequestSyncAsync(string providerKey, CancellationToken cancellationToken)
    {
        if (providerKey is null || !JobsByProvider.TryGetValue(providerKey, out var job))
        {
            return Error.NotFound("Analytics.UnknownProvider", "Unknown analytics provider.");
        }

        if (!await jobs.RequestRunAsync(job, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Analytics.JobUnavailable", "The Worker has not registered this sync job yet, or it is disabled.");
        }

        auditTrail.Record(AuditAction.AnalyticsSyncRequested, AuditCategory.Integrations, "ProviderConnection", providerKey, new { job });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
