using Onyx.Contracts.Common;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>Inquiries are immutable; their consent records are the only part that changes (marketing withdrawal).</summary>
public interface IInquiryRepository
{
    Task<Inquiry?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Inquiry>> ListForLeadAsync(Guid leadId, CancellationToken cancellationToken);

    /// <summary>Inquiries of leads the viewer may see (all when <paramref name="ownerScope"/> is null), newest first.</summary>
    Task<PagedResult<Inquiry>> ListAsync(InquiryType? type, LeadOwnerScope? ownerScope, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<ConsentRecord>> ListConsentsForLeadAsync(Guid leadId, CancellationToken cancellationToken);

    Task<ConsentRecord?> GetConsentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The leads (of those given) with at least one marketing consent that is given and not withdrawn.</summary>
    Task<IReadOnlySet<Guid>> ListLeadsWithMarketingConsentAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken cancellationToken);

    void Add(Inquiry inquiry, ConsentRecord consent);
}
