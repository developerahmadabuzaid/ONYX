using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.Web.ApiClients;

public interface IConsentApiClient
{
    Task<ApiResult<AcceptedResponse>> RecordAsync(RecordCookieConsentRequest request, CancellationToken cancellationToken);
}
