namespace Onyx.Contracts.Delivery;

/// <summary>Adds a member or changes their role (the project version guards concurrent edits).</summary>
public sealed record SaveProjectMemberRequest(
    Guid UserId,
    string? Role,
    uint Version);
