namespace Onyx.Contracts.Booking;

public sealed record BookingSettingsResponse(
    bool IsEnabled,
    bool IsBookable,
    string TimeZoneId,
    int SlotMinutes,
    int BufferMinutes,
    int MinimumNoticeHours,
    int HorizonDays,
    uint Version,
    IReadOnlyList<AvailabilityRuleResponse> Rules,
    IReadOnlyList<AvailabilityExceptionResponse> Exceptions);
