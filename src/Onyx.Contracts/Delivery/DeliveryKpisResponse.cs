namespace Onyx.Contracts.Delivery;

/// <summary>
/// Headline figures. "InRange" counts fall inside the selected period; "PreviousRange" counts fall in the period of the same
/// length just before it, so a change is shown only where both are real.
/// </summary>
public sealed record DeliveryKpisResponse(
    int TotalProjects,
    int ProjectsCreatedInRange,
    int ActiveProjects,
    int TotalIssues,
    int CreatedInRange,
    int CreatedPreviousRange,
    int CompletedIssues,
    int CompletedInRange,
    int CompletedPreviousRange,
    int PendingIssues,
    int InProgressIssues,
    int OverdueIssues,
    int AssignedToMe);
