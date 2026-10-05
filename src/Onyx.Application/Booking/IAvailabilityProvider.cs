namespace Onyx.Application.Booking;

/// <summary>
/// Free consultation slots (architecture.md §13.4). The default implementation is internal, admin-managed availability; a
/// calendar-backed provider can replace it later without Domain or Application referencing any calendar product.
/// </summary>
public interface IAvailabilityProvider
{
    Task<AvailabilitySnapshot> GetAvailabilityAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The free slot that starts exactly at <paramref name="startUtc"/>, or null. <paramref name="ignoreBookingId"/> treats that
    /// booking as free (used when rescheduling it).
    /// </summary>
    Task<BookableSlot?> FindSlotAsync(DateTimeOffset startUtc, Guid? ignoreBookingId, CancellationToken cancellationToken);
}
