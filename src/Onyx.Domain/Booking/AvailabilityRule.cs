using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Booking;

/// <summary>A weekly window of consultation hours, in the business time zone (e.g. Sunday 10:00-14:00).</summary>
public sealed class AvailabilityRule : Entity, IAuditable
{
    private AvailabilityRule()
    {
    }

    public DayOfWeek DayOfWeek { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Leads;

    public static Result<AvailabilityRule> Create(DayOfWeek dayOfWeek, TimeOnly start, TimeOnly end)
    {
        if (!Enum.IsDefined(dayOfWeek) || end <= start || start.Second != 0 || end.Second != 0 || start.Minute % 5 != 0 || end.Minute % 5 != 0)
        {
            return Error.Validation("Availability.InvalidRule", "Each window needs a day and a start before its end, on 5-minute boundaries.");
        }

        return new AvailabilityRule { DayOfWeek = dayOfWeek, StartTime = start, EndTime = end };
    }

    public bool Overlaps(AvailabilityRule other) => DayOfWeek == other.DayOfWeek && StartTime < other.EndTime && other.StartTime < EndTime;
}
