using Onyx.Application.Booking;
using Onyx.Domain.Booking;

namespace Onyx.Application.Tests.Booking;

public sealed class SlotCalculatorTests
{
    // Sunday 2026-10-04 06:00 UTC = 09:00 in Riyadh (UTC+3, no daylight saving).
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Closed_or_unconfigured_booking_offers_no_slots()
    {
        Assert.Empty(SlotCalculator.Calculate(BookingSettings.CreateDisabled(), [Rule(DayOfWeek.Sunday, 9, 17)], [], [], Now));
        Assert.Empty(SlotCalculator.Calculate(Settings(60, 0, 0, 7), [], [], [], Now));
    }

    [Fact]
    public void Slots_follow_the_weekly_window_in_the_business_time_zone()
    {
        var slots = SlotCalculator.Calculate(Settings(60, 0, 0, 1), [Rule(DayOfWeek.Sunday, 9, 12)], [], [], Now);

        Assert.Equal(
            [new DateTimeOffset(2026, 10, 4, 6, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 4, 7, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero)],
            slots.Select(slot => slot.StartUtc));
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromHours(3), slot.StartLocal.Offset));
        Assert.Equal(9, slots[0].StartLocal.Hour);
    }

    [Fact]
    public void Notice_buffer_busy_slots_blocked_dates_and_horizon_are_respected()
    {
        var rules = new[] { Rule(DayOfWeek.Sunday, 9, 13), Rule(DayOfWeek.Monday, 9, 13) };
        var busy = new[] { (new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero)) };

        // 1h notice drops 09:00; the booking 11:00–12:00 plus its 30-minute buffer blocks the 11:00 and 12:00 starts.
        var sunday = SlotCalculator.Calculate(Settings(30, 30, 1, 1), rules, [new DateOnly(2026, 10, 5)], busy, Now);

        Assert.Equal(["10:00"], sunday.Select(slot => slot.StartLocal.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.DoesNotContain(sunday, slot => slot.StartLocal.DayOfWeek == DayOfWeek.Monday);
    }

    [Fact]
    public void Daylight_saving_gaps_are_skipped()
    {
        var settings = BookingSettings.CreateDisabled();
        settings.Update(true, "Europe/Berlin", 60, 0, 0, 7);
        var beforeChange = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

        // 2026-03-29 02:00–03:00 does not exist in Berlin.
        var slots = SlotCalculator.Calculate(settings, [Rule(DayOfWeek.Sunday, 1, 4)], [], [], beforeChange);

        Assert.Equal([1, 3], slots.Where(slot => slot.StartLocal.Day == 29).Select(slot => slot.StartLocal.Hour));
    }

    private static BookingSettings Settings(int slot, int buffer, int noticeHours, int horizonDays)
    {
        var settings = BookingSettings.CreateDisabled();
        Assert.True(settings.Update(true, "Asia/Riyadh", slot, buffer, noticeHours, horizonDays).IsSuccess);
        return settings;
    }

    private static AvailabilityRule Rule(DayOfWeek day, int fromHour, int toHour) =>
        AvailabilityRule.Create(day, new TimeOnly(fromHour, 0), new TimeOnly(toHour, 0)).Value;
}
