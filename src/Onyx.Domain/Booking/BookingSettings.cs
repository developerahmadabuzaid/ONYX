using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Booking;

/// <summary>
/// Singleton settings for consultation booking (architecture.md §13.4). The working hours, slot length, buffer, notice and
/// horizon are business inputs entered by an administrator; until booking is enabled and weekly hours exist, no slots are
/// offered and visitors use the consultation request form instead.
/// </summary>
public sealed class BookingSettings : AuditableEntity, IAuditable
{
    public const string DefaultTimeZoneId = "Asia/Riyadh";
    public const string SingletonKey = "default";
    public const int MinSlotMinutes = 15;
    public const int MaxSlotMinutes = 240;
    public const int MaxBufferMinutes = 120;
    public const int MaxNoticeHours = 24 * 14;
    public const int MaxHorizonDays = 180;

    private BookingSettings()
    {
        TimeZoneId = DefaultTimeZoneId;
    }

    /// <summary>Always <see cref="SingletonKey"/>; a unique index on it keeps the settings a single row.</summary>
    public string Key { get; private set; } = SingletonKey;

    public bool IsEnabled { get; private set; }

    public string TimeZoneId { get; private set; }

    public int SlotMinutes { get; private set; }

    public int BufferMinutes { get; private set; }

    public int MinimumNoticeHours { get; private set; }

    public int HorizonDays { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Leads;

    /// <summary>The disabled starting point: no slot length or horizon until an administrator sets them.</summary>
    public static BookingSettings CreateDisabled() => new();

    public Result Update(bool isEnabled, string timeZoneId, int slotMinutes, int bufferMinutes, int minimumNoticeHours, int horizonDays)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            return Error.Validation("Booking.InvalidTimeZone", "Choose a valid IANA time zone, for example Asia/Riyadh.");
        }

        if (slotMinutes is < MinSlotMinutes or > MaxSlotMinutes || slotMinutes % 5 != 0)
        {
            return Error.Validation("Booking.InvalidSlotLength", $"Slots are {MinSlotMinutes} to {MaxSlotMinutes} minutes, in steps of 5.");
        }

        if (bufferMinutes is < 0 or > MaxBufferMinutes || minimumNoticeHours is < 0 or > MaxNoticeHours || horizonDays is < 1 or > MaxHorizonDays)
        {
            return Error.Validation(
                "Booking.InvalidLimits", $"Buffer is 0-{MaxBufferMinutes} minutes, notice 0-{MaxNoticeHours} hours and horizon 1-{MaxHorizonDays} days.");
        }

        IsEnabled = isEnabled;
        TimeZoneId = timeZoneId.Trim();
        SlotMinutes = slotMinutes;
        BufferMinutes = bufferMinutes;
        MinimumNoticeHours = minimumNoticeHours;
        HorizonDays = horizonDays;
        return Result.Success();
    }

    /// <summary>Booking is offered only when it is switched on and its slot length and horizon have been set.</summary>
    public bool IsBookable => IsEnabled && SlotMinutes >= MinSlotMinutes && HorizonDays >= 1;
}
