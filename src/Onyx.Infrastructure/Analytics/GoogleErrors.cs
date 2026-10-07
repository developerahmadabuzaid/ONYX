using System.Net;
using Google;
using Google.Apis.Auth.OAuth2.Responses;
using Onyx.Application.Analytics;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Analytics;

/// <summary>Maps Google client failures to safe codes. Provider messages, request details and credentials are never passed on.</summary>
internal static class GoogleErrors
{
    public static Error Map(Exception exception) => exception switch
    {
        TokenResponseException => Unavailable(AnalyticsProviderErrors.Unauthorized),
        GoogleApiException { HttpStatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => Unavailable(AnalyticsProviderErrors.Unauthorized),
        GoogleApiException { HttpStatusCode: HttpStatusCode.TooManyRequests } => Unavailable(AnalyticsProviderErrors.QuotaExceeded),
        GoogleApiException { HttpStatusCode: HttpStatusCode.BadRequest } => Unavailable(AnalyticsProviderErrors.InvalidResponse),
        InvalidOperationException or ArgumentException or IOException or Newtonsoft.Json.JsonException => Unavailable(AnalyticsProviderErrors.Unauthorized),
        TaskCanceledException or TimeoutException => Unavailable(AnalyticsProviderErrors.Timeout),
        FormatException => Unavailable(AnalyticsProviderErrors.InvalidResponse),
        _ => Unavailable(AnalyticsProviderErrors.Unavailable),
    };

    private static Error Unavailable(string code) => Error.Unavailable(code, "The analytics provider request failed.");
}
