using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Email;
using Onyx.Application.Leads;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Domain.Booking;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Booking;

/// <summary>Staff view of consultation bookings: list, cancel (optionally telling the visitor) and record the outcome.</summary>
public sealed class BookingAdministrationService(
    IBookingRepository bookings,
    ILeadRepository leads,
    IInquiryRepository inquiries,
    IEmailOutbox emailOutbox,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IBookingAdministrationService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("Booking.NotFound", "Booking not found.");

    public async Task<Result<PagedResult<BookingResponse>>> ListAsync(BookingListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        BookingStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!EnumParsing.TryParse<BookingStatus>(query.Status, out var parsed))
            {
                return Error.Validation("Booking.InvalidStatus", "Unknown booking status.");
            }

            status = parsed;
        }

        var page = await bookings.ListAsync(
            query.Upcoming, status, timeProvider.GetUtcNow(), Math.Max(query.Page, 1), Math.Clamp(query.PageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        var names = await LeadNamesAsync(page.Items.Select(booking => booking.LeadId), cancellationToken).ConfigureAwait(false);
        return new PagedResult<BookingResponse>(page.Items.Select(booking => ToResponse(booking, names)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<BookingResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var booking = await bookings.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return booking is null ? NotFound : ToResponse(booking, await LeadNamesAsync([booking.LeadId], cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<BookingResponse>> CancelAsync(Guid id, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var booking = await bookings.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return NotFound;
        }

        if (request.Version is { } version)
        {
            bookings.SetExpectedVersion(booking, version);
        }

        var now = timeProvider.GetUtcNow();
        var cancelled = booking.Cancel(byVisitor: false, now);
        if (cancelled.IsFailure)
        {
            return cancelled.Error!;
        }

        await RecordActivityAsync(booking, LeadActivityKind.BookingCancelled, "staff", now, cancellationToken).ConfigureAwait(false);
        if (request.NotifyVisitor && await inquiries.GetAsync(booking.InquiryId, cancellationToken).ConfigureAwait(false) is { } inquiry)
        {
            var queued = await emailOutbox.QueueAsync(BookingMessages.Cancelled(booking, inquiry.Email, inquiry.Name, now), cancellationToken).ConfigureAwait(false);
            if (queued.IsFailure)
            {
                return queued.Error!;
            }
        }

        auditTrail.Record(AuditAction.BookingStatusChanged, AuditCategory.Leads, nameof(ConsultationBooking), booking.Id.ToString(), new { status = booking.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(booking, await LeadNamesAsync([booking.LeadId], cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<BookingResponse>> RecordOutcomeAsync(Guid id, RecordBookingOutcomeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<BookingStatus>(request.Outcome, out var outcome))
        {
            return Error.Validation("Booking.InvalidOutcome", "The outcome is Completed or NoShow.");
        }

        var booking = await bookings.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (booking is null)
        {
            return NotFound;
        }

        if (request.Version is { } version)
        {
            bookings.SetExpectedVersion(booking, version);
        }

        var now = timeProvider.GetUtcNow();
        var recorded = booking.RecordOutcome(outcome, now);
        if (recorded.IsFailure)
        {
            return recorded.Error!;
        }

        await RecordActivityAsync(
            booking, outcome == BookingStatus.Completed ? LeadActivityKind.BookingCompleted : LeadActivityKind.BookingNoShow, null, now, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.BookingStatusChanged, AuditCategory.Leads, nameof(ConsultationBooking), booking.Id.ToString(), new { status = booking.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(booking, await LeadNamesAsync([booking.LeadId], cancellationToken).ConfigureAwait(false));
    }

    private async Task RecordActivityAsync(ConsultationBooking booking, LeadActivityKind kind, string? detail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await leads.GetAsync(booking.LeadId, cancellationToken).ConfigureAwait(false) is { } lead)
        {
            lead.RecordActivity(kind, detail, booking.Id, currentUser.UserId, now);
            leads.AddPendingActivities(lead);
        }
    }

    private Task<IReadOnlyDictionary<Guid, string>> LeadNamesAsync(IEnumerable<Guid> leadIds, CancellationToken cancellationToken) =>
        leads.GetDisplayNamesAsync(leadIds.Distinct().ToList(), cancellationToken);

    private static BookingResponse ToResponse(ConsultationBooking booking, IReadOnlyDictionary<Guid, string> leadNames)
    {
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(booking.TimeZoneId, out var zone) ? TimeZoneInfo.ConvertTime(booking.StartUtc, zone) : booking.StartUtc;
        return new BookingResponse(
            booking.Id, booking.LeadId, leadNames.TryGetValue(booking.LeadId, out var name) ? name : string.Empty, booking.StartUtc, booking.EndUtc, local,
            booking.TimeZoneId, booking.Status.ToString(), booking.LanguageCode, booking.RescheduledFromId, booking.RescheduledToId, booking.CancelledAtUtc,
            booking.CancelledByVisitor, booking.Version);
    }
}
