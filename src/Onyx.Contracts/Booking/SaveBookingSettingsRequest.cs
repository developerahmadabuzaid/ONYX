namespace Onyx.Contracts.Booking;

public sealed record SaveBookingSettingsRequest(
    bool IsEnabled,
    string TimeZoneId,
    int SlotMinutes,
    int BufferMinutes,
    int MinimumNoticeHours,
    int HorizonDays,
    uint? Version);
