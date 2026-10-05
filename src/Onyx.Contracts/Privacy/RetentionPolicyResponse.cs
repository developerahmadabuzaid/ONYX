namespace Onyx.Contracts.Privacy;

/// <summary>
/// One category. <see cref="IsSaved"/> is false until an administrator saves it; <see cref="IsActive"/> (enabled with a period)
/// is the only state in which the enforcement job acts.
/// </summary>
public sealed record RetentionPolicyResponse(
    string Category,
    string Action,
    int? PeriodDays,
    bool IsEnabled,
    bool IsActive,
    bool IsSaved,
    IReadOnlyList<string> AllowedActions,
    uint? Version);
