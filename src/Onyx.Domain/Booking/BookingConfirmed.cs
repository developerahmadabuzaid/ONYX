using Onyx.Domain.Common;

namespace Onyx.Domain.Booking;

/// <summary>A slot was booked (new or as a reschedule). The Worker sends the internal email and the admin notification.</summary>
public sealed record BookingConfirmed(Guid BookingId, Guid LeadId, Guid? RescheduledFromId) : IDomainEvent;
