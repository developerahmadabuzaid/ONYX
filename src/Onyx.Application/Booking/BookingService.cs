using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Leads;
using Onyx.Contracts.Booking;
using Onyx.Domain.Booking;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Booking;

/// <summary>
/// Consultation booking (architecture.md §13.4). One transaction stores the booking, its Inquiry(Consultation), the lead
/// (created or matched), the visitor confirmation email with the calendar file and the outbox events. Double booking is
/// prevented by the slot check here and, for concurrent requests, by the unique index on confirmed start times (409).
/// The visitor manage link carries a random 256-bit token; only its SHA-256 hash is stored.
/// </summary>
public sealed class BookingService(
    IAvailabilityProvider availability,
    IBookingRepository bookings,
    IInquiryRepository inquiries,
    ILeadRepository leads,
    InquiryIntake intake,
    InquiryRecorder recorder,
    BookingMessages messages,
    IEmailOutbox emailOutbox,
    LeadSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IBookingService
{
    public const string CaptchaAction = "booking";

    /// <summary>Conflict code the persistence layer reports when the unique index on confirmed slot start times rejects a booking.</summary>
    public const string SlotTakenCode = "Booking.SlotTaken";

    private static readonly Error SlotUnavailable = Error.Conflict("Booking.SlotUnavailable", "That time is no longer available. Choose another slot.");
    private static readonly Error BookingClosed = Error.Conflict("Booking.Closed", "Online booking is not available. Use the consultation request form instead.");
    private static readonly Error InvalidLink = Error.NotFound("Booking.LinkInvalid", "This booking link is not valid or the booking is no longer active.");
    private static readonly Error NotManageable = Error.Conflict("Booking.NotManageable", "This booking can no longer be changed online.");

    public async Task<AvailabilityResponse> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var snapshot = await availability.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        return new AvailabilityResponse(
            snapshot.Bookable,
            snapshot.TimeZoneId,
            snapshot.SlotMinutes,
            snapshot.Slots.Select(slot => new AvailableSlotResponse(slot.StartUtc, slot.EndUtc, slot.StartLocal)).ToList());
    }

    public async Task<Result<BookingConfirmedResponse>> BookAsync(BookConsultationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Form is null)
        {
            return Error.Validation("Booking.MissingForm", "The booking form is missing.");
        }

        var snapshot = await availability.GetAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        if (!snapshot.Bookable)
        {
            return BookingClosed;
        }

        var prepared = await intake.PrepareAsync(InquiryType.Consultation, request.Form, CaptchaAction, cancellationToken).ConfigureAwait(false);
        if (prepared.IsFailure)
        {
            return prepared.Error!;
        }

        var startUtc = request.StartUtc.ToUniversalTime();
        var slot = snapshot.Slots.FirstOrDefault(candidate => candidate.StartUtc == startUtc);
        if (prepared.Value.IsSpam)
        {
            return slot is null
                ? SlotUnavailable
                : new BookingConfirmedResponse(InquiryRecorder.Reference(Guid.CreateVersion7()), slot.StartUtc, slot.EndUtc, slot.StartLocal, snapshot.TimeZoneId);
        }

        if (slot is null)
        {
            return SlotUnavailable;
        }

        var recorded = await recorder.RecordAsync(prepared.Value, cancellationToken).ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        var (inquiry, lead) = recorded.Value;
        var now = timeProvider.GetUtcNow();
        var token = PreviewTokenCodec.NewToken();
        var booking = ConsultationBooking.Confirm(
            slot.StartUtc, slot.EndUtc, snapshot.TimeZoneId, lead.Id, inquiry.Id, inquiry.LanguageCode, PreviewTokenCodec.Hash(token),
            slot.EndUtc.Add(settings.ManageLinkLifetime), null, now);
        if (booking.IsFailure)
        {
            return booking.Error!;
        }

        bookings.Add(booking.Value);
        lead.RecordActivity(LeadActivityKind.BookingConfirmed, null, booking.Value.Id, null, now);
        leads.AddPendingActivities(lead);

        var queued = await emailOutbox.QueueAsync(messages.Confirmed(booking.Value, inquiry.Email, inquiry.Name, token, now), cancellationToken).ConfigureAwait(false);
        if (queued.IsFailure)
        {
            return queued.Error!;
        }

        if (await SaveAsync(cancellationToken).ConfigureAwait(false) is { IsFailure: true } failed)
        {
            return failed.Error!;
        }

        return new BookingConfirmedResponse(InquiryRecorder.Reference(inquiry.Id), slot.StartUtc, slot.EndUtc, slot.StartLocal, snapshot.TimeZoneId);
    }

    public async Task<Result<ManagedBookingResponse>> GetManagedAsync(ManageBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindByTokenAsync(request?.Token, cancellationToken).ConfigureAwait(false);
        return booking is null ? InvalidLink : ToManaged(booking);
    }

    public async Task<Result<ManagedBookingResponse>> CancelAsync(ManageBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindByTokenAsync(request?.Token, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return InvalidLink;
        }

        var now = timeProvider.GetUtcNow();
        if (!booking.CanBeManagedByVisitor(now))
        {
            return NotManageable;
        }

        var cancelled = booking.Cancel(byVisitor: true, now);
        if (cancelled.IsFailure)
        {
            return cancelled.Error!;
        }

        await RecordLeadActivityAsync(booking.LeadId, LeadActivityKind.BookingCancelled, "visitor", booking.Id, now, cancellationToken).ConfigureAwait(false);
        if (await inquiries.GetAsync(booking.InquiryId, cancellationToken).ConfigureAwait(false) is { } inquiry)
        {
            var queued = await emailOutbox.QueueAsync(BookingMessages.Cancelled(booking, inquiry.Email, inquiry.Name, now), cancellationToken).ConfigureAwait(false);
            if (queued.IsFailure)
            {
                return queued.Error!;
            }
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? ToManaged(booking) : saved.Error!;
    }

    public async Task<Result<ManagedBookingResponse>> RescheduleAsync(RescheduleBookingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var current = await FindByTokenAsync(request.Token, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            return InvalidLink;
        }

        var now = timeProvider.GetUtcNow();
        if (!current.CanBeManagedByVisitor(now))
        {
            return NotManageable;
        }

        var startUtc = request.StartUtc.ToUniversalTime();
        if (startUtc == current.StartUtc)
        {
            return Error.Validation("Booking.SameSlot", "Choose a different time to reschedule.");
        }

        var slot = await availability.FindSlotAsync(startUtc, current.Id, cancellationToken).ConfigureAwait(false);
        if (slot is null)
        {
            return SlotUnavailable;
        }

        // The visitor keeps the same link: the token hash moves to the replacement booking.
        var replacement = ConsultationBooking.Confirm(
            slot.StartUtc, slot.EndUtc, current.TimeZoneId, current.LeadId, current.InquiryId, current.LanguageCode, PreviewTokenCodec.Hash(request.Token),
            slot.EndUtc.Add(settings.ManageLinkLifetime), current.Id, now);
        if (replacement.IsFailure)
        {
            return replacement.Error!;
        }

        var replaced = current.MarkRescheduled(replacement.Value.Id);
        if (replaced.IsFailure)
        {
            return replaced.Error!;
        }

        bookings.Add(replacement.Value);
        await RecordLeadActivityAsync(current.LeadId, LeadActivityKind.BookingRescheduled, null, replacement.Value.Id, now, cancellationToken).ConfigureAwait(false);
        if (await inquiries.GetAsync(current.InquiryId, cancellationToken).ConfigureAwait(false) is { } inquiry)
        {
            var queued = await emailOutbox.QueueAsync(messages.Confirmed(replacement.Value, inquiry.Email, inquiry.Name, request.Token, now), cancellationToken)
                .ConfigureAwait(false);
            if (queued.IsFailure)
            {
                return queued.Error!;
            }
        }

        var saved = await SaveAsync(cancellationToken).ConfigureAwait(false);
        return saved.IsSuccess ? ToManaged(replacement.Value) : saved.Error!;
    }

    public async Task<Result<CalendarFileResponse>> GetCalendarFileAsync(ManageBookingRequest request, CancellationToken cancellationToken)
    {
        var booking = await FindByTokenAsync(request?.Token, cancellationToken).ConfigureAwait(false);
        return booking is null
            ? InvalidLink
            : new CalendarFileResponse(IcsCalendar.FileName, BookingMessages.CalendarFile(booking, timeProvider.GetUtcNow()));
    }

    private async Task<ConsultationBooking?> FindByTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (!PreviewTokenCodec.IsWellFormed(token))
        {
            return null;
        }

        var booking = await bookings.FindByManageTokenHashAsync(PreviewTokenCodec.Hash(token!), cancellationToken).ConfigureAwait(false);
        return booking is { IsActive: true } && timeProvider.GetUtcNow() < booking.ManageTokenExpiresAtUtc ? booking : null;
    }

    private async Task RecordLeadActivityAsync(Guid leadId, LeadActivityKind kind, string? detail, Guid relatedId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await leads.GetAsync(leadId, cancellationToken).ConfigureAwait(false) is { } lead)
        {
            lead.RecordActivity(kind, detail, relatedId, null, now);
            leads.AddPendingActivities(lead);
        }
    }

    /// <summary>Saves; a concurrent booking of the same slot (rejected by the unique index) becomes <see cref="SlotUnavailable"/>.</summary>
    private async Task<Result> SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (ConcurrencyConflictException conflict) when (conflict.ConflictCode == SlotTakenCode)
        {
            return SlotUnavailable;
        }
    }

    private ManagedBookingResponse ToManaged(ConsultationBooking booking)
    {
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(booking.TimeZoneId, out var zone) ? TimeZoneInfo.ConvertTime(booking.StartUtc, zone) : booking.StartUtc;
        return new ManagedBookingResponse(
            booking.StartUtc, booking.EndUtc, local, booking.TimeZoneId, booking.Status.ToString(), booking.LanguageCode,
            booking.CanBeManagedByVisitor(timeProvider.GetUtcNow()));
    }
}
