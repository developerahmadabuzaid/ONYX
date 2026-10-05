namespace Onyx.Contracts.Leads;

/// <summary>An active admin user who may own leads (holds <c>Leads.View</c>).</summary>
public sealed record LeadAssigneeResponse(Guid UserId, string DisplayName);
