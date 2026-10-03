using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;

namespace Onyx.Web.ApiClients;

public sealed class ConsentApiClient(HttpClient httpClient) : OnyxApiClientBase(httpClient), IConsentApiClient
{
    public Task<ApiResult<AcceptedResponse>> RecordAsync(RecordCookieConsentRequest request, CancellationToken cancellationToken) =>
        PostAsync<AcceptedResponse>("api/v1/consents", request, cancellationToken);
}
