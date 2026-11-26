namespace Onyx.Domain.Delivery;

/// <summary>The editable details of a delivery project (everything except its key, owner and workflow).</summary>
public sealed record DeliveryProjectDetails(
    string? Name,
    string? Description,
    DeliveryProjectType Type,
    DeliveryClassification Classification,
    string? ClientName,
    DeliveryProjectStatus Status,
    DeliveryPriority Priority,
    DateOnly? StartDate,
    DateOnly? TargetDate,
    string? RepositoryUrl);
