using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Providers;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Common;

namespace Onyx.API.Controllers.Admin;

/// <summary>Provider status and connection tests. Credentials are configured in the secret store, never through this API.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/provider-connections")]
[HasPermission(Permissions.ProviderConnections.Manage)]
public sealed class AdminProviderConnectionsController(IProviderConnectionAdministrationService providers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProviderConnectionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProviderConnectionResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await providers.ListAsync(cancellationToken));

    [HttpPost("{providerKey}/disable")]
    [ProducesResponseType<ProviderConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Disable(string providerKey, CancellationToken cancellationToken) =>
        this.OkOrProblem(await providers.DisableAsync(providerKey, cancellationToken));

    [HttpPost("{providerKey}/enable")]
    [ProducesResponseType<ProviderConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiProblem>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Enable(string providerKey, CancellationToken cancellationToken) =>
        this.OkOrProblem(await providers.EnableAsync(providerKey, cancellationToken));

    /// <summary>Queues a test email to the current admin's own address; the Worker sends it and updates the provider status.</summary>
    [HttpPost("email/test")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult> QueueEmailTest(CancellationToken cancellationToken)
    {
        var result = await providers.QueueEmailTestAsync(cancellationToken);
        return result.IsSuccess ? Accepted(new AcceptedResponse(result.Value)) : this.Problem(result.Error!);
    }
}
