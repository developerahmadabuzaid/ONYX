using Microsoft.EntityFrameworkCore;
using Onyx.Application.Analytics;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>First-party funnel facts: projections of leads, inquiries and bookings (no names, emails, phones or messages).</summary>
internal sealed class AnalyticsFunnelRepository(ApplicationDbContext dbContext) : IAnalyticsFunnelRepository
{
    public async Task<IReadOnlyList<LeadFunnelFact>> ListLeadFactsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int max, CancellationToken cancellationToken)
    {
        var leads = await dbContext.Leads.AsNoTracking()
            .Where(lead => lead.FirstInquiryAtUtc >= fromUtc && lead.FirstInquiryAtUtc < toUtc)
            .OrderBy(lead => lead.FirstInquiryAtUtc)
            .Take(max)
            .Select(lead => new { lead.Id, lead.Stage })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (leads.Count == 0)
        {
            return [];
        }

        var ids = leads.Select(lead => lead.Id).ToList();
        var inquiries = await dbContext.Inquiries.AsNoTracking()
            .Where(inquiry => ids.Contains(inquiry.LeadId))
            .Select(inquiry => new { inquiry.LeadId, inquiry.SubmittedAtUtc, inquiry.Type, inquiry.ServiceId, inquiry.LanguageCode, inquiry.Attribution })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var first = inquiries.GroupBy(inquiry => inquiry.LeadId).ToDictionary(group => group.Key, group => group.OrderBy(inquiry => inquiry.SubmittedAtUtc).First());
        return leads
            .Where(lead => first.ContainsKey(lead.Id))
            .Select(lead =>
            {
                var inquiry = first[lead.Id];
                return new LeadFunnelFact(lead.Id, lead.Stage, inquiry.Type, inquiry.ServiceId, inquiry.LanguageCode, inquiry.Attribution);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<InquiryFact>> ListInquiryFactsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, int max, CancellationToken cancellationToken) =>
        (await dbContext.Inquiries.AsNoTracking()
            .Where(inquiry => inquiry.SubmittedAtUtc >= fromUtc && inquiry.SubmittedAtUtc < toUtc)
            .OrderBy(inquiry => inquiry.SubmittedAtUtc)
            .Take(max)
            .Select(inquiry => new { inquiry.Type, inquiry.ServiceId, inquiry.LanguageCode, inquiry.Attribution })
            .ToListAsync(cancellationToken).ConfigureAwait(false))
        .Select(inquiry => new InquiryFact(inquiry.Type, inquiry.ServiceId, inquiry.LanguageCode, inquiry.Attribution))
        .ToList();

    public Task<long> CountBookingsAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        dbContext.ConsultationBookings.LongCountAsync(booking => booking.CreatedAtUtc >= fromUtc && booking.CreatedAtUtc < toUtc, cancellationToken);
}
