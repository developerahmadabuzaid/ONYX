using Onyx.Domain.Booking;

namespace Onyx.Domain.Tests.Booking;

public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', ConsultationBooking.HashLength);

    [Fact]
    public void Settings_start_closed_and_validate_every_limit()
    {
        var settings = BookingSettings.CreateDisabled();
        Assert.False(settings.IsBookable);
        Assert.Equal("Asia/Riyadh", settings.TimeZoneId);

        Assert.True(settings.Update(true, "Not/AZone", 30, 0, 0, 7).IsFailure);
        Assert.True(settings.Update(true, "Asia/Riyadh", 10, 0, 0, 7).IsFailure);
        Assert.True(settings.Update(true, "Asia/Riyadh", 32, 0, 0, 7).IsFailure);
        Assert.True(settings.Update(true, "Asia/Riyadh", 30, -1, 0, 7).IsFailure);
        Assert.True(settings.Update(true, "Asia/Riyadh", 30, 0, 0, 0).IsFailure);
        Assert.False(settings.IsBookable);

        Assert.True(settings.Update(true, "Asia/Riyadh", 30, 10, 24, 14).IsSuccess);
        Assert.True(settings.IsBookable);
        Assert.True(settings.Update(false, "Asia/Riyadh", 30, 10, 24, 14).IsSuccess);
        Assert.False(settings.IsBookable);
    }

    [Fact]
    public void Weekly_windows_must_be_ordered_and_overlaps_are_detected()
    {
        Assert.True(AvailabilityRule.Create(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(9, 0)).IsFailure);
        Assert.True(AvailabilityRule.Create(DayOfWeek.Sunday, new TimeOnly(9, 7), new TimeOnly(12, 0)).IsFailure);

        var morning = AvailabilityRule.Create(DayOfWeek.Sunday, new TimeOnly(9, 0), new TimeOnly(12, 0)).Value;
        Assert.True(morning.Overlaps(AvailabilityRule.Create(DayOfWeek.Sunday, new TimeOnly(11, 0), new TimeOnly(13, 0)).Value));
        Assert.False(morning.Overlaps(AvailabilityRule.Create(DayOfWeek.Sunday, new TimeOnly(12, 0), new TimeOnly(13, 0)).Value));
        Assert.False(morning.Overlaps(AvailabilityRule.Create(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)).Value));
    }

    [Fact]
    public void A_booking_is_a_future_utc_range_with_a_hashed_manage_token()
    {
        Assert.True(Confirm(Now.AddHours(-1)).IsFailure);
        Assert.True(ConsultationBooking.Confirm(
            new DateTimeOffset(2026, 10, 6, 9, 0, 0, TimeSpan.FromHours(3)), Now.AddDays(2), "Asia/Riyadh", Guid.NewGuid(), Guid.NewGuid(), "en", Hash, Now.AddDays(3), null, Now).IsFailure);
        Assert.Throws<ArgumentException>(() => ConsultationBooking.Confirm(
            Now.AddDays(1), Now.AddDays(1).AddMinutes(30), "Asia/Riyadh", Guid.NewGuid(), Guid.NewGuid(), "en", "plain-token", Now.AddDays(2), null, Now));

        var booking = Confirm(Now.AddDays(1)).Value;
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.IsType<BookingConfirmed>(Assert.Single(booking.DomainEvents));
        Assert.True(booking.CanBeManagedByVisitor(Now));
        Assert.False(booking.CanBeManagedByVisitor(Now.AddDays(1)));
    }

    [Fact]
    public void Cancel_reschedule_and_outcome_follow_the_state_rules()
    {
        var cancelled = Confirm(Now.AddDays(1)).Value;
        Assert.True(cancelled.Cancel(byVisitor: true, Now).IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.ManageTokenHash);
        Assert.Contains(cancelled.DomainEvents, domainEvent => domainEvent is BookingCancelled { ByVisitor: true });
        Assert.True(cancelled.Cancel(byVisitor: false, Now).IsFailure);
        Assert.True(cancelled.MarkRescheduled(Guid.NewGuid()).IsFailure);

        var moved = Confirm(Now.AddDays(1)).Value;
        var replacement = Guid.NewGuid();
        Assert.True(moved.MarkRescheduled(replacement).IsSuccess);
        Assert.Equal(BookingStatus.Rescheduled, moved.Status);
        Assert.Equal(replacement, moved.RescheduledToId);
        Assert.Null(moved.ManageTokenHash);

        var attended = Confirm(Now.AddDays(1)).Value;
        Assert.True(attended.RecordOutcome(BookingStatus.Completed, Now).IsFailure);
        Assert.True(attended.RecordOutcome(BookingStatus.Cancelled, Now.AddDays(2)).IsFailure);
        Assert.True(attended.RecordOutcome(BookingStatus.NoShow, Now.AddDays(1)).IsSuccess);
        Assert.Equal(BookingStatus.NoShow, attended.Status);
    }

    [Fact]
    public void Blocked_date_reasons_are_bounded()
    {
        Assert.True(AvailabilityException.Create(new DateOnly(2026, 12, 2), new string('x', 201)).IsFailure);
        Assert.Null(AvailabilityException.Create(new DateOnly(2026, 12, 2), " ").Value.Reason);
    }

    private static Onyx.Domain.Common.Result<ConsultationBooking> Confirm(DateTimeOffset start) =>
        ConsultationBooking.Confirm(start, start.AddMinutes(30), "Asia/Riyadh", Guid.NewGuid(), Guid.NewGuid(), "en", Hash, start.AddDays(1), null, Now);
}
