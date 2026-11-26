namespace Onyx.Contracts.Delivery;

/// <summary>What the caller may do in a project, resolved on the server from their project role or workspace permission. The UI only hides; the API enforces.</summary>
public sealed record DeliveryCapabilities(
    bool View,
    bool CreateIssues,
    bool Comment,
    bool EditIssues,
    bool ManageSprints,
    bool ManageProject);
