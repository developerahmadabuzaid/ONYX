namespace Onyx.Contracts.Leads;

/// <summary>A lead with its inquiries (each with its origin trail and consent), notes, timeline and bookings.</summary>
public sealed record LeadDetailResponse(
    LeadSummaryResponse Summary,
    Guid? PillarId,
    IReadOnlyList<string> ScoreReasons,
    DateTimeOffset? ClosedAtUtc,
    IReadOnlyList<InquiryResponse> Inquiries,
    IReadOnlyList<LeadNoteResponse> Notes,
    IReadOnlyList<LeadActivityResponse> Activities,
    IReadOnlyList<LeadBookingResponse> Bookings);
