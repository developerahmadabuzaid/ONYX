namespace Onyx.Contracts.Delivery;

/// <summary>An allowed status change.</summary>
public sealed record WorkflowTransitionResponse(
    Guid FromStatusId,
    Guid ToStatusId);
