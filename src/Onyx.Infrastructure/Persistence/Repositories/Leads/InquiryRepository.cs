using Microsoft.EntityFrameworkCore;
using Onyx.Application.Leads;
using Onyx.Contracts.Common;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class InquiryRepository(ApplicationDbContext dbContext) : IInquiryRepository
{
    public Task<Inquiry?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Inquiries.AsNoTracking().SingleOrDefaultAsync(inquiry => inquiry.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Inquiry>> ListForLeadAsync(Guid leadId, CancellationToken cancellationToken) =>
        await dbContext.Inquiries.AsNoTracking()
            .Where(inquiry => inquiry.LeadId == leadId)
            .OrderBy(inquiry => inquiry.SubmittedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<PagedResult<Inquiry>> ListAsync(InquiryType? type, LeadOwnerScope? ownerScope, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Inquiries.AsNoTracking();
        if (type is { } wanted)
        {
            query = query.Where(inquiry => inquiry.Type == wanted);
        }

        if (ownerScope is { } scope)
        {
            query = query.Where(inquiry => dbContext.Leads.Any(lead => lead.Id == inquiry.LeadId && (lead.OwnerId == null || lead.OwnerId == scope.UserId)));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(inquiry => inquiry.SubmittedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<Inquiry>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<ConsentRecord>> ListConsentsForLeadAsync(Guid leadId, CancellationToken cancellationToken) =>
        await dbContext.ConsentRecords.AsNoTracking().Where(consent => consent.LeadId == leadId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ConsentRecord?> GetConsentAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ConsentRecords.SingleOrDefaultAsync(consent => consent.Id == id, cancellationToken);

    public async Task<IReadOnlySet<Guid>> ListLeadsWithMarketingConsentAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken)
    {
        if (leadIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = await dbContext.ConsentRecords.AsNoTracking()
            .Where(consent => leadIds.Contains(consent.LeadId) && consent.MarketingConsent && consent.MarketingWithdrawnAtUtc == null)
            .Select(consent => consent.LeadId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids.ToHashSet();
    }

    public void Add(Inquiry inquiry, ConsentRecord consent)
    {
        dbContext.Inquiries.Add(inquiry);
        dbContext.ConsentRecords.Add(consent);
    }
}
