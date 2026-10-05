using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>The default availability provider: admin-managed booking settings, weekly windows and blocked dates.</summary>
public sealed class InternalAvailabilityProvider(IAvailabilityRepository availability, IBookingRepository bookings, TimeProvider timeProvider)
    : IAvailabilityProvider
{
    public Task<AvailabilitySnapshot> GetAvailabilityAsync(CancellationToken cancellationToken) => CalculateAsync(null, cancellationToken);

    public async Task<BookableSlot?> FindSlotAsync(DateTimeOffset startUtc, Guid? ignoreBookingId, CancellationToken cancellationToken)
    {
        var snapshot = await CalculateAsync(ignoreBookingId, cancellationToken).ConfigureAwait(false);
        return snapshot.Slots.FirstOrDefault(slot => slot.StartUtc == startUtc);
    }

    private async Task<AvailabilitySnapshot> CalculateAsync(Guid? ignoreBookingId, CancellationToken cancellationToken)
    {
        var settings = await availability.GetSettingsAsync(cancellationToken).ConfigureAwait(false);
        if (settings is not { IsBookable: true })
        {
            return AvailabilitySnapshot.Closed(settings?.TimeZoneId ?? BookingSettings.DefaultTimeZoneId);
        }

        var now = timeProvider.GetUtcNow();
        var rules = await availability.ListRulesAsync(cancellationToken).ConfigureAwait(false);

        // Blocked dates are local business dates; start a day early so the zone offset never hides today's exception.
        var exceptions = await availability.ListExceptionsAsync(DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1), cancellationToken).ConfigureAwait(false);
        var busy = await bookings.ListBusyAsync(now, cancellationToken).ConfigureAwait(false);
        var slots = SlotCalculator.Calculate(
            settings,
            rules,
            exceptions.Select(exception => exception.Date).ToList(),
            busy.Where(booking => booking.Id != ignoreBookingId).Select(booking => (booking.StartUtc, booking.EndUtc)).ToList(),
            now);
        return new AvailabilitySnapshot(true, settings.TimeZoneId, settings.SlotMinutes, slots);
    }
}
