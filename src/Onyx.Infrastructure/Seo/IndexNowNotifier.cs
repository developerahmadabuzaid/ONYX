using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.Providers;
using Onyx.Application.Seo;
using Onyx.Domain.Providers;

namespace Onyx.Infrastructure.Seo;

/// <summary>
/// IndexNow adapter (https://www.indexnow.org/documentation). Active only when a valid key is configured, production indexing is
/// enabled (<c>Site:AllowIndexing</c>) and the public origin is an https URL. Submits absolute, percent-encoded URLs of this host
/// only; the key file is served by Onyx.Web at <see cref="KeyFilePath"/>. Transient refusals are retried by the outbox.
/// The key is never logged.
/// </summary>
internal sealed partial class IndexNowNotifier(
    HttpClient httpClient,
    IOptions<IndexNowOptions> options,
    IOptions<PublicSiteOptions> site,
    IProviderConnectionService providers,
    ILogger<IndexNowNotifier> logger) : ISearchEngineNotifier
{
    public const string KeyFilePath = "/indexnow-key.txt";
    public const int MaxUrlsPerRequest = 10_000;

    public bool IsActive => site.Value.AllowIndexing && site.Value.Origin is not null && IsValidKey(options.Value.Key)
        && Uri.TryCreate(options.Value.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == Uri.UriSchemeHttps;

    public string? VerificationKey => IsActive ? options.Value.Key : null;

    public static bool IsValidKey(string? key) => key is not null && KeyPattern().IsMatch(key);

    /// <summary>Absolute URL for a site path: each segment is NFC-normalized and percent-encoded; a trailing slash is kept.</summary>
    public static string AbsoluteUrl(Uri origin, string sitePath)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(sitePath);
        var segments = sitePath.Normalize(NormalizationForm.FormC).Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString);
        var path = "/" + string.Join('/', segments);
        if (sitePath.EndsWith('/') && path.Length > 1)
        {
            path += "/";
        }

        return origin.GetLeftPart(UriPartial.Authority) + path;
    }

    public async Task NotifyAsync(IReadOnlyCollection<string> sitePaths, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sitePaths);
        if (!IsActive || site.Value.Origin is not { } origin)
        {
            return;
        }

        var urls = sitePaths
            .Where(path => path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal))
            .Select(path => AbsoluteUrl(origin, path))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var batch in urls.Chunk(MaxUrlsPerRequest))
        {
            await SubmitAsync(origin, batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SubmitAsync(Uri origin, IReadOnlyList<string> urls, CancellationToken cancellationToken)
    {
        var body = new
        {
            host = origin.IdnHost,
            key = options.Value.Key,
            keyLocation = origin.GetLeftPart(UriPartial.Authority) + KeyFilePath,
            urlList = urls,
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(new Uri(options.Value.Endpoint, UriKind.Absolute), body, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            await providers.ReportFailureAsync(ProviderKeys.IndexNow, ProviderCategory.SearchEngineNotification, "IndexNow endpoint unreachable.", cancellationToken).ConfigureAwait(false);
            throw new SearchEngineNotificationDeferredException("IndexNow endpoint unreachable.", exception);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            LogSubmission(logger, urls.Count, status);
            if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted)
            {
                await providers.ReportSuccessAsync(ProviderKeys.IndexNow, ProviderCategory.SearchEngineNotification, cancellationToken).ConfigureAwait(false);
                return;
            }

            var error = $"IndexNow answered {status}.";
            await providers.ReportFailureAsync(ProviderKeys.IndexNow, ProviderCategory.SearchEngineNotification, error, cancellationToken).ConfigureAwait(false);

            // Rate limits and server errors are temporary: let the outbox retry with backoff. Key or URL problems (400/403/422)
            // are configuration errors that a retry cannot fix; they surface through the provider status instead.
            if (response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500)
            {
                throw new SearchEngineNotificationDeferredException(error);
            }
        }
    }

    [GeneratedRegex("^[a-zA-Z0-9-]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "IndexNow submission of {UrlCount} URL(s) answered {StatusCode}")]
    private static partial void LogSubmission(ILogger logger, int urlCount, int statusCode);
}
