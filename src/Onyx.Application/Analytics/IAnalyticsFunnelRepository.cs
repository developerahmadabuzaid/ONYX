using Onyx.Domain.Leads;

namespace Onyx.Application.Analytics;

/// <summary>
/// First-party facts for the lead funnel (architecture.md §15): Onyx's own inquiries, leads and bookings with their Stage 8
/// attribution. Projections only, never contact details; bounded by date range and a row cap.
/// </summary>
public interface IAnalyticsFunnelRepository
{
    /// <summary>Leads whose first inquiry falls in the range, with their current stage and that first inquiry's attribution.</summary>
    Task<IReadOnlyList<LeadFunnelFact>> ListLeadFactsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int max, CancellationToken cancellationToken);

    /// <summary>Inquiries submitted in the range (any lead).</summary>
    Task<IReadOnlyList<InquiryFact>> ListInquiryFactsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int max, CancellationToken cancellationToken);

    /// <summary>Consultation bookings created in the range (each one confirmed on creation).</summary>
    Task<long> CountBookingsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

public sealed record LeadFunnelFact(Guid LeadId, LeadStage Stage, InquiryType FirstInquiryType, Guid? ServiceId, string LanguageCode, Attribution Attribution);

public sealed record InquiryFact(InquiryType Type, Guid? ServiceId, string LanguageCode, Attribution Attribution);
