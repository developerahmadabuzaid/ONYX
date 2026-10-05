using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Leads;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Booking;
using Onyx.Domain.Leads;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Booking;

/// <summary>
/// Worker side of a new or rescheduled booking: calendar provider hook, internal email and the NewBooking notification. The
/// visitor confirmation was already queued with the booking (it carries the manage link). Idempotent per booking.
/// </summary>
public sealed class BookingConfirmedHandler(
    IBookingRepository bookings,
    IProcessedEventLog processed,
    IBookingCalendarProvider calendar,
    LeadNotifier notifier,
    INotificationService notifications) : IDomainEventHandler<BookingConfirmed>
{
    public async Task HandleAsync(BookingConfirmed domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var booking = await bookings.GetAsync(domainEvent.BookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null || !await processed.TryBeginAsync($"booking-confirmed:{booking.Id:N}", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await calendar.BookingConfirmedAsync(booking, cancellationToken).ConfigureAwait(false);

        var values = BookingMessages.SlotValues(booking);
        values["status"] = domainEvent.RescheduledFromId is null ? "Confirmed" : "Rescheduled";
        values["reference"] = InquiryRecorder.Reference(booking.InquiryId);
        values["language"] = booking.LanguageCode;
        await notifier.QueueInternalAsync(InquiryType.Consultation, BookingMessages.InternalTemplate, booking.LeadId, values, cancellationToken).ConfigureAwait(false);

        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.NewBooking,
                NotificationSeverity.Info,
                Permissions.Bookings.View,
                new Dictionary<string, string> { ["date"] = values["date"], ["time"] = values["time"], ["status"] = values["status"] },
                ResourceType: "Booking",
                ResourceId: booking.Id.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
