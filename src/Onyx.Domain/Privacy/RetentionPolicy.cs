using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Privacy;

/// <summary>
/// How long one data category is kept and what happens after (architecture.md §14, D20). Periods are business/legal inputs from
/// the approved privacy policy, so nothing is preset: a category without a saved, enabled policy with a period is never
/// processed. Disabling a policy keeps its settings.
/// </summary>
public sealed class RetentionPolicy : AuditableEntity, IAuditable
{
    public const int MaxPeriodDays = 36_500;

    private RetentionPolicy()
    {
    }

    public RetentionCategory Category { get; private set; }

    public RetentionAction Action { get; private set; }

    /// <summary>Records older than this many days are processed; null until set from the approved policy.</summary>
    public int? PeriodDays { get; private set; }

    public bool IsEnabled { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Privacy;

    /// <summary>Enabled with a period: the enforcement job may act on this category.</summary>
    public bool IsActive => IsEnabled && PeriodDays is not null;

    /// <summary>The actions each category supports; anonymizing applies only where records carry separable personal fields.</summary>
    public static IReadOnlySet<RetentionAction> AllowedActions(RetentionCategory category) => category switch
    {
        RetentionCategory.LeadsAndInquiries or RetentionCategory.AuditLogs => new HashSet<RetentionAction> { RetentionAction.Delete, RetentionAction.Anonymize },
        _ => new HashSet<RetentionAction> { RetentionAction.Delete },
    };

    public static Result<RetentionPolicy> Create(RetentionCategory category, RetentionAction action, int? periodDays, bool isEnabled)
    {
        if (!Enum.IsDefined(category))
        {
            return Error.Validation("Retention.InvalidCategory", "Unknown retention category.");
        }

        var policy = new RetentionPolicy { Category = category };
        var updated = policy.Update(action, periodDays, isEnabled);
        return updated.IsFailure ? updated.Error! : policy;
    }

    public Result Update(RetentionAction action, int? periodDays, bool isEnabled)
    {
        if (!Enum.IsDefined(action) || !AllowedActions(Category).Contains(action))
        {
            return Error.Validation("Retention.ActionNotAllowed", $"{Category} supports: {string.Join(", ", AllowedActions(Category))}.");
        }

        if (periodDays is < 1 or > MaxPeriodDays)
        {
            return Error.Validation("Retention.InvalidPeriod", $"The period is 1 to {MaxPeriodDays} days.");
        }

        if (isEnabled && periodDays is null)
        {
            return Error.Validation("Retention.PeriodRequired", "Set the period from the approved privacy policy before enabling the policy.");
        }

        Action = action;
        PeriodDays = periodDays;
        IsEnabled = isEnabled;
        return Result.Success();
    }

    /// <summary>Records created before this instant are due; null when the policy is not active.</summary>
    public DateTimeOffset? CutoffAt(DateTimeOffset now) => IsActive ? now.AddDays(-PeriodDays!.Value) : null;
}
