namespace Onyx.Application.Leads;

/// <summary>Restricts lead queries for users without <c>Leads.ViewAll</c> to their own and unassigned leads.</summary>
public sealed record LeadOwnerScope(Guid UserId);
