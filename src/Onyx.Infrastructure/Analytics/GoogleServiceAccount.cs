using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Analytics;

/// <summary>
/// The owner's Google service account, loaded only from the configured secret (JSON or a mounted key file) and only as a
/// service-account key: <see cref="CredentialFactory.FromJson{T}"/> refuses every other credential type (for example external
/// account configurations that would make the library fetch arbitrary URLs). Credentials are scoped to read-only APIs and are
/// never logged, stored or returned; token caching and refresh are handled by the Google library in memory.
/// </summary>
internal sealed class GoogleServiceAccount(IOptions<GoogleAnalyticsOptions> options)
{
    public const string ApplicationName = "OnyxVanguard-Analytics";

    private readonly Lock _gate = new();
    private ServiceAccountCredential? _credential;

    public GoogleAnalyticsOptions Options => options.Value;

    public BaseClientService.Initializer Initializer(params string[] scopes)
    {
        var credential = Load().ToGoogleCredential().CreateScoped(scopes);
        return new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName,
            GZipEnabled = true,
            DefaultExponentialBackOffPolicy = ExponentialBackOffPolicy.Exception | ExponentialBackOffPolicy.UnsuccessfulResponse503,
        };
    }

    private ServiceAccountCredential Load()
    {
        lock (_gate)
        {
            if (_credential is not null)
            {
                return _credential;
            }

            var json = !string.IsNullOrWhiteSpace(Options.ServiceAccountKeyJson)
                ? Options.ServiceAccountKeyJson
                : File.ReadAllText(Options.ServiceAccountKeyFile ?? throw new InvalidOperationException("No service-account key is configured."));
            _credential = CredentialFactory.FromJson<ServiceAccountCredential>(json);
            return _credential;
        }
    }
}
