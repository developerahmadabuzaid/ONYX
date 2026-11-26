namespace Onyx.Contracts.Delivery;

/// <summary>A user who can be added to projects (holds Delivery.Access).</summary>
public sealed record DeliveryUserResponse(
    Guid UserId,
    string DisplayName);
