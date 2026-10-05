using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// A validated lead query. <see cref="OwnerId"/> filters to one owner; <see cref="UnassignedOnly"/> to leads without an owner;
/// <see cref="Scope"/> applies the viewer visibility rule on top.
/// </summary>
public sealed record LeadFilter(LeadStage? Stage, Guid? OwnerId, bool UnassignedOnly, string? Search, LeadOwnerScope? Scope, int Page, int PageSize);
