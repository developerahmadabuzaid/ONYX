using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Leads;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Booking;
using Onyx.Domain.Leads;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Booking;

/// <summary>Worker side of a cancellation: calendar provider hook, internal email and a BookingCancelled notification. Idempotent.</summary>
public sealed class BookingCancelledHandler(
    IBookingRepository bookings,
    IProcessedEventLog processed,
    IBookingCalendarProvider calendar,
    LeadNotifier notifier,
    INotificationService notifications) : IDomainEventHandler<BookingCancelled>
{
    public async Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var booking = await bookings.GetAsync(domainEvent.BookingId, cancellationToken).ConfigureAwait(false);
        if (booking is null || !await processed.TryBeginAsync($"booking-cancelled:{booking.Id:N}", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await calendar.BookingCancelledAsync(booking, cancellationToken).ConfigureAwait(false);

        var values = BookingMessages.SlotValues(booking);
        values["status"] = domainEvent.ByVisitor ? "CancelledByVisitor" : "CancelledByStaff";
        values["reference"] = InquiryRecorder.Reference(booking.InquiryId);
        values["language"] = booking.LanguageCode;
        await notifier.QueueInternalAsync(InquiryType.Consultation, BookingMessages.InternalTemplate, booking.LeadId, values, cancellationToken).ConfigureAwait(false);

        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.BookingCancelled,
                NotificationSeverity.Warning,
                Permissions.Bookings.View,
                new Dictionary<string, string> { ["date"] = values["date"], ["time"] = values["time"] },
                ResourceType: "Booking",
                ResourceId: booking.Id.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
