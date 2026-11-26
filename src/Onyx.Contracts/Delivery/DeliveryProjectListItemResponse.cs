namespace Onyx.Contracts.Delivery;

/// <summary>A project row in the projects list, with issue counts from the database.</summary>
public sealed record DeliveryProjectListItemResponse(
    Guid Id,
    string Key,
    string Name,
    string Type,
    string Classification,
    string? ClientName,
    string Status,
    string Priority,
    DateOnly? TargetDate,
    string? MyRole,
    int TotalIssues,
    int OpenIssues,
    int DoneIssues);
