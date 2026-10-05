using Onyx.Contracts.Leads;
using Onyx.Domain.Common;

namespace Onyx.Application.Leads;

public interface IInquiryService
{
    /// <summary>Stores a public form submission. Success never depends on email delivery (emails are queued by the Worker).</summary>
    Task<Result<InquiryAcceptedResponse>> SubmitAsync(SubmitInquiryRequest request, CancellationToken cancellationToken);
}
