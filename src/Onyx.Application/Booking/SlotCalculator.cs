using Onyx.Domain.Booking;

namespace Onyx.Application.Booking;

/// <summary>
/// Builds the slot grid from the weekly windows in the business time zone, then removes blocked dates, slots inside the minimum
/// notice, slots beyond the horizon and slots that touch a confirmed booking (including the buffer on both sides). Local times
/// that do not exist (a daylight-saving gap) are skipped; slots are computed in local time and stored in UTC.
/// </summary>
public static class SlotCalculator
{
    public static IReadOnlyList<BookableSlot> Calculate(
        BookingSettings settings,
        IReadOnlyCollection<AvailabilityRule> rules,
        IReadOnlyCollection<DateOnly> blockedDates,
        IReadOnlyCollection<(DateTimeOffset StartUtc, DateTimeOffset EndUtc)> busy,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsBookable || rules.Count == 0 || !TimeZoneInfo.TryFindSystemTimeZoneById(settings.TimeZoneId, out var zone))
        {
            return [];
        }

        var slotLength = TimeSpan.FromMinutes(settings.SlotMinutes);
        var buffer = TimeSpan.FromMinutes(settings.BufferMinutes);
        var earliest = nowUtc.AddHours(settings.MinimumNoticeHours);
        var latest = nowUtc.AddDays(settings.HorizonDays);
        var blocked = blockedDates.ToHashSet();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(nowUtc, zone).DateTime);
        var slots = new List<BookableSlot>();

        for (var date = today; date <= DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(latest, zone).DateTime); date = date.AddDays(1))
        {
            if (blocked.Contains(date))
            {
                continue;
            }

            foreach (var rule in rules.Where(rule => rule.DayOfWeek == date.DayOfWeek).OrderBy(rule => rule.StartTime))
            {
                var windowEnd = date.ToDateTime(rule.EndTime);
                for (var localStart = date.ToDateTime(rule.StartTime); localStart + slotLength <= windowEnd; localStart += slotLength + buffer)
                {
                    if (zone.IsInvalidTime(localStart))
                    {
                        continue;
                    }

                    var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, zone), TimeSpan.Zero);
                    var endUtc = startUtc + slotLength;
                    if (startUtc < earliest || startUtc > latest
                        || busy.Any(booking => startUtc < booking.EndUtc + buffer && booking.StartUtc - buffer < endUtc))
                    {
                        continue;
                    }

                    slots.Add(new BookableSlot(startUtc, endUtc, TimeZoneInfo.ConvertTime(startUtc, zone)));
                }
            }
        }

        return slots.OrderBy(slot => slot.StartUtc).ToList();
    }
}
