namespace Onyx.Contracts.Delivery;

/// <summary>How a bulk change went: how many issues changed, and why the others did not.</summary>
public sealed record BulkIssueResultResponse(int Succeeded, IReadOnlyList<BulkIssueFailureResponse> Failed);
