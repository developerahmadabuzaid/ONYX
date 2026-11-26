using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Subscriptions;

/// <summary>
/// A customer's subscription to a product (docs/architecture.md §24.8). Requested → Active or Rejected (staff decide);
/// Trial → Active, Expired (Worker) or Cancelled; Active → Cancelled. A customer has at most one open subscription per product
/// and at most one trial per product, ever (both also enforced by partial unique indexes).
/// </summary>
public sealed class CustomerSubscription : AuditableEntity, IAuditable
{
    public const int NoteMaxLength = 1000;

    private CustomerSubscription()
    {
    }

    public Guid CustomerId { get; private set; }

    public Guid ProductId { get; private set; }

    public Guid? PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public bool IsTrial { get; private set; }

    public DateTimeOffset RequestedAtUtc { get; private set; }

    public DateTimeOffset? TrialEndsAtUtc { get; private set; }

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    /// <summary>End of the paid term, when one was agreed; null means open-ended until cancelled.</summary>
    public DateOnly? EndsOn { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    [SensitiveData]
    public string? CustomerNote { get; private set; }

    public string? StaffNote { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Commerce;

    public bool IsOpen => Status is SubscriptionStatus.Requested or SubscriptionStatus.Trial or SubscriptionStatus.Active;

    public static Result<CustomerSubscription> Request(Guid customerId, Guid productId, Guid? planId, string? note, DateTimeOffset nowUtc)
    {
        var cleanNote = CleanNote(note);
        if (cleanNote.IsFailure)
        {
            return cleanNote.Error!;
        }

        return new CustomerSubscription
        {
            CustomerId = customerId,
            ProductId = productId,
            PlanId = planId,
            Status = SubscriptionStatus.Requested,
            RequestedAtUtc = nowUtc,
            CustomerNote = cleanNote.Value,
        };
    }

    public static Result<CustomerSubscription> StartTrial(Guid customerId, Guid productId, Guid? planId, int trialDays, DateTimeOffset nowUtc)
    {
        if (trialDays < 1)
        {
            return Error.Conflict("Subscription.NoTrial", "This product has no trial.");
        }

        return new CustomerSubscription
        {
            CustomerId = customerId,
            ProductId = productId,
            PlanId = planId,
            Status = SubscriptionStatus.Trial,
            IsTrial = true,
            RequestedAtUtc = nowUtc,
            TrialEndsAtUtc = nowUtc.AddDays(trialDays),
        };
    }

    public Result Activate(DateOnly? endsOn, string? staffNote, DateTimeOffset nowUtc)
    {
        if (Status is not (SubscriptionStatus.Requested or SubscriptionStatus.Trial))
        {
            return Error.Conflict("Subscription.InvalidTransition", "Only a requested or trial subscription can be activated.");
        }

        if (endsOn is { } end && end <= DateOnly.FromDateTime(nowUtc.UtcDateTime))
        {
            return Error.Validation("Subscription.InvalidEndDate", "The end date must be in the future.");
        }

        var note = CleanNote(staffNote);
        if (note.IsFailure)
        {
            return note.Error!;
        }

        Status = SubscriptionStatus.Active;
        ActivatedAtUtc = nowUtc;
        EndsOn = endsOn;
        StaffNote = note.Value ?? StaffNote;
        return Result.Success();
    }

    public Result Reject(string? staffNote, DateTimeOffset nowUtc)
    {
        if (Status != SubscriptionStatus.Requested)
        {
            return Error.Conflict("Subscription.InvalidTransition", "Only a requested subscription can be rejected.");
        }

        var note = CleanNote(staffNote);
        if (note.IsFailure)
        {
            return note.Error!;
        }

        Status = SubscriptionStatus.Rejected;
        StaffNote = note.Value;
        ClosedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Cancel(DateTimeOffset nowUtc)
    {
        if (!IsOpen)
        {
            return Error.Conflict("Subscription.InvalidTransition", "This subscription is already closed.");
        }

        Status = SubscriptionStatus.Cancelled;
        ClosedAtUtc = nowUtc;
        return Result.Success();
    }

    /// <summary>Ends a trial whose end date has passed; returns false (no change) otherwise, so the Worker job is idempotent.</summary>
    public bool ExpireTrial(DateTimeOffset nowUtc)
    {
        if (Status != SubscriptionStatus.Trial || TrialEndsAtUtc is not { } end || nowUtc < end)
        {
            return false;
        }

        Status = SubscriptionStatus.Expired;
        ClosedAtUtc = nowUtc;
        return true;
    }

    private static Result<string?> CleanNote(string? note)
    {
        var clean = note?.Trim();
        if (string.IsNullOrEmpty(clean))
        {
            return Result.Success<string?>(null);
        }

        return clean.Length <= NoteMaxLength ? clean : Error.Validation("Subscription.NoteTooLong", $"Notes are at most {NoteMaxLength} characters.");
    }
}
