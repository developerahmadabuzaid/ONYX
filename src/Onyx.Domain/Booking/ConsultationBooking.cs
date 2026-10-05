using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Booking;

/// <summary>
/// A booked consultation slot (architecture.md §13.4), stored in UTC. A unique index on the start time of confirmed bookings
/// is the final guard against double booking. The visitor manages the booking through a signed link: only the SHA-256 hash of
/// its random token is stored, and the token moves to the replacement booking on reschedule.
/// </summary>
public sealed class ConsultationBooking : AuditableEntity, IAuditable
{
    public const int HashLength = 64;
    public const int CancelReasonMaxLength = 300;

    private ConsultationBooking()
    {
        ManageTokenHash = string.Empty;
        LanguageCode = string.Empty;
        TimeZoneId = string.Empty;
    }

    public DateTimeOffset StartUtc { get; private set; }

    public DateTimeOffset EndUtc { get; private set; }

    /// <summary>The business time zone the slot was offered in, for display in emails and the calendar file.</summary>
    public string TimeZoneId { get; private set; }

    public BookingStatus Status { get; private set; }

    public Guid LeadId { get; private set; }

    public Guid InquiryId { get; private set; }

    public string LanguageCode { get; private set; }

    public string? ManageTokenHash { get; private set; }

    public DateTimeOffset ManageTokenExpiresAtUtc { get; private set; }

    public Guid? RescheduledFromId { get; private set; }

    public Guid? RescheduledToId { get; private set; }

    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public bool CancelledByVisitor { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Leads;

    public bool IsActive => Status == BookingStatus.Confirmed;

    public static Result<ConsultationBooking> Confirm(
        DateTimeOffset startUtc, DateTimeOffset endUtc, string timeZoneId, Guid leadId, Guid inquiryId, string languageCode, string manageTokenHash,
        DateTimeOffset manageTokenExpiresAtUtc, Guid? rescheduledFromId, DateTimeOffset now)
    {
        if (startUtc.Offset != TimeSpan.Zero || endUtc <= startUtc || startUtc <= now)
        {
            return Error.Validation("Booking.InvalidSlot", "The slot must be a future UTC time range.");
        }

        if (manageTokenHash is not { Length: HashLength } || manageTokenExpiresAtUtc <= now)
        {
            throw new ArgumentException("A booking needs a SHA-256 manage-token hash and a future expiry.");
        }

        var booking = new ConsultationBooking
        {
            StartUtc = startUtc,
            EndUtc = endUtc,
            TimeZoneId = timeZoneId,
            Status = BookingStatus.Confirmed,
            LeadId = leadId,
            InquiryId = inquiryId,
            LanguageCode = languageCode,
            ManageTokenHash = manageTokenHash,
            ManageTokenExpiresAtUtc = manageTokenExpiresAtUtc,
            RescheduledFromId = rescheduledFromId,
        };
        booking.RaiseDomainEvent(new BookingConfirmed(booking.Id, leadId, rescheduledFromId));
        return booking;
    }

    /// <summary>Whether the visitor's manage link still works: the booking is upcoming and confirmed and the token is current.</summary>
    public bool CanBeManagedByVisitor(DateTimeOffset now) => IsActive && ManageTokenHash is not null && now < ManageTokenExpiresAtUtc && now < StartUtc;

    public Result Cancel(bool byVisitor, DateTimeOffset now)
    {
        if (!IsActive)
        {
            return Error.Conflict("Booking.NotActive", "Only a confirmed booking can be cancelled.");
        }

        Status = BookingStatus.Cancelled;
        CancelledAtUtc = now;
        CancelledByVisitor = byVisitor;
        ManageTokenHash = null;
        RaiseDomainEvent(new BookingCancelled(Id, LeadId, byVisitor));
        return Result.Success();
    }

    /// <summary>Marks this booking replaced by <paramref name="replacementId"/>; the manage token moves to the replacement.</summary>
    public Result MarkRescheduled(Guid replacementId)
    {
        if (!IsActive)
        {
            return Error.Conflict("Booking.NotActive", "Only a confirmed booking can be rescheduled.");
        }

        Status = BookingStatus.Rescheduled;
        RescheduledToId = replacementId;
        ManageTokenHash = null;
        return Result.Success();
    }

    /// <summary>Staff record the outcome once the slot has started.</summary>
    public Result RecordOutcome(BookingStatus outcome, DateTimeOffset now)
    {
        if (outcome is not (BookingStatus.Completed or BookingStatus.NoShow))
        {
            return Error.Validation("Booking.InvalidOutcome", "The outcome is Completed or NoShow.");
        }

        if (!IsActive || now < StartUtc)
        {
            return Error.Conflict("Booking.OutcomeTooEarly", "An outcome can be recorded only for a confirmed booking whose time has started.");
        }

        Status = outcome;
        ManageTokenHash = null;
        return Result.Success();
    }
}
