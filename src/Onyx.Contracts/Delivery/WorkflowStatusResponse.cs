namespace Onyx.Contracts.Delivery;

/// <summary>A workflow status; SystemKey is set for default statuses so the UI can localize their names.</summary>
public sealed record WorkflowStatusResponse(
    Guid Id,
    string Name,
    string? SystemKey,
    string Category,
    int Order,
    int? WipLimit);
