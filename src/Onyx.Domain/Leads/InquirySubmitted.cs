using Onyx.Domain.Common;

namespace Onyx.Domain.Leads;

/// <summary>An inquiry was stored (architecture.md §13.3). The Worker sends the internal and visitor emails and the admin notification.</summary>
public sealed record InquirySubmitted(Guid InquiryId, Guid LeadId) : IDomainEvent;
