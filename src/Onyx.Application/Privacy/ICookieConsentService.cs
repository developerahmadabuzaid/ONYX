using Onyx.Contracts.Privacy;
using Onyx.Domain.Common;

namespace Onyx.Application.Privacy;

public interface ICookieConsentService
{
    Task<Result<Guid>> RecordAsync(RecordCookieConsentRequest request, CancellationToken cancellationToken);
}
