using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Analytics;
using Onyx.Contracts.Analytics;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>
/// GA4 and Search Console operations (<c>ProviderConnections.Manage</c>, 2FA-privileged): status and data coverage, and a
/// "sync now" request that only makes the Worker job due. No credential, token or account identifier is ever returned.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/analytics-integrations")]
[HasPermission(Permissions.ProviderConnections.Manage)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminAnalyticsIntegrationsController(AnalyticsIntegrationService integrations) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AnalyticsIntegrationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalyticsIntegrationResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await integrations.ListAsync(cancellationToken));

    [HttpPost("{providerKey}/sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RequestSync(string providerKey, CancellationToken cancellationToken)
    {
        var result = await integrations.RequestSyncAsync(providerKey, cancellationToken);
        return result.IsSuccess ? Accepted() : this.Problem(result.Error!);
    }
}
