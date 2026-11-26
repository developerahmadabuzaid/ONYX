namespace Onyx.Contracts.Delivery;

/// <summary>Open issues and open story points assigned to one person (visible projects only).</summary>
public sealed record WorkloadResponse(
    Guid UserId,
    string DisplayName,
    int OpenIssues,
    int OpenPoints);
