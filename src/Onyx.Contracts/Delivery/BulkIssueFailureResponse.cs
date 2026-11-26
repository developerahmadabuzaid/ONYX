namespace Onyx.Contracts.Delivery;

/// <summary>An issue a bulk change could not apply to, with the reason.</summary>
public sealed record BulkIssueFailureResponse(Guid IssueId, string? Key, string Code, string Message);
