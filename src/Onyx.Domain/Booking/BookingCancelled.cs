using Onyx.Domain.Common;

namespace Onyx.Domain.Booking;

/// <summary>A booking was cancelled by the visitor (signed link) or by staff.</summary>
public sealed record BookingCancelled(Guid BookingId, Guid LeadId, bool ByVisitor) : IDomainEvent;
