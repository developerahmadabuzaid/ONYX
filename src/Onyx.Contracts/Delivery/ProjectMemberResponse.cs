namespace Onyx.Contracts.Delivery;

/// <summary>A project member.</summary>
public sealed record ProjectMemberResponse(
    Guid UserId,
    string DisplayName,
    string Role);
