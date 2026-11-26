namespace Onyx.Contracts.Delivery;

/// <summary>Create (Version null) or plan (Version required) a sprint.</summary>
public sealed record SaveSprintRequest(
    string? Name,
    string? Goal,
    DateOnly? StartDate,
    DateOnly? EndDate,
    uint? Version);
