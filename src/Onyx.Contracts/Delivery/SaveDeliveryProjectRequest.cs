namespace Onyx.Contracts.Delivery;

/// <summary>Create (Key required, Version null) or update (Key ignored, Version required) a project.</summary>
public sealed record SaveDeliveryProjectRequest(
    string? Key,
    string? Name,
    string? Description,
    string? Type,
    string? Classification,
    string? ClientName,
    string? Status,
    string? Priority,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    string? RepositoryUrl,
    uint? Version);
