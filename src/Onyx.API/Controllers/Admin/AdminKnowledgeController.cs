using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Onyx.API.Authorization;
using Onyx.API.Http;
using Onyx.Application.Knowledge;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Knowledge;

namespace Onyx.API.Controllers.Admin;

/// <summary>Approved Company Knowledge and "needs information" requests. Proposing and verifying are separate permissions.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/admin/knowledge")]
public sealed class AdminKnowledgeController(IKnowledgeService knowledge) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Knowledge.View)]
    [ProducesResponseType<IReadOnlyList<KnowledgeEntryResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] string? category, [FromQuery] string? status, [FromQuery] string? search, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.ListAsync(category, status, search, cancellationToken));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Knowledge.View)]
    [ProducesResponseType<KnowledgeEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken cancellationToken) => this.OkOrProblem(await knowledge.GetAsync(id, cancellationToken));

    [HttpPost]
    [HasPermission(Permissions.Knowledge.Edit)]
    [ProducesResponseType<KnowledgeEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Create(SaveKnowledgeEntryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.CreateAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Knowledge.Edit)]
    [ProducesResponseType<KnowledgeEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, SaveKnowledgeEntryRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [HasPermission(Permissions.Knowledge.Approve)]
    [ProducesResponseType<KnowledgeEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Approve(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.ApproveAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/retire")]
    [HasPermission(Permissions.Knowledge.Approve)]
    [ProducesResponseType<KnowledgeEntryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Retire(Guid id, KnowledgeVersionRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.RetireAsync(id, request, cancellationToken));

    [HttpGet("governance-rules")]
    [HasPermission(Permissions.Knowledge.View)]
    [ProducesResponseType<GovernanceRulesResponse>(StatusCodes.Status200OK)]
    public ActionResult<GovernanceRulesResponse> GovernanceRules() => Ok(knowledge.GetGovernanceRules());

    [HttpGet("information-requests")]
    [HasPermission(Permissions.Knowledge.View)]
    [ProducesResponseType<IReadOnlyList<InformationRequestResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> InformationRequests([FromQuery] string? status, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.ListRequestsAsync(status, cancellationToken));

    [HttpPost("information-requests")]
    [HasPermission(Permissions.Knowledge.View)]
    [ProducesResponseType<InformationRequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RaiseInformationRequest(RaiseInformationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.RaiseRequestAsync(request, cancellationToken));

    [HttpPost("information-requests/{id:guid}/resolve")]
    [HasPermission(Permissions.Knowledge.Approve)]
    [ProducesResponseType<InformationRequestResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResolveInformationRequest(Guid id, ResolveInformationRequest request, CancellationToken cancellationToken) =>
        this.OkOrProblem(await knowledge.ResolveRequestAsync(id, request, cancellationToken));
}
