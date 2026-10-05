namespace Onyx.Domain.Leads;

public enum LeadActivityKind
{
    InquiryReceived = 0,
    StageChanged = 1,
    Assigned = 2,
    PriorityChanged = 3,
    NoteAdded = 4,
    BookingConfirmed = 5,
    BookingCancelled = 6,
    BookingRescheduled = 7,
    BookingCompleted = 8,
    BookingNoShow = 9,
    MarketingConsentWithdrawn = 10,
    Erased = 11,
}
