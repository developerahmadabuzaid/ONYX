namespace Onyx.Contracts.Leads;

/// <summary>
/// Lead list filters. <see cref="Owner"/> is <c>me</c>, <c>unassigned</c> or a user id. <see cref="Search"/> matches name,
/// company or email.
/// </summary>
public sealed record LeadListQuery(string? Stage = null, string? Owner = null, string? Search = null, int Page = 1, int PageSize = 25);
