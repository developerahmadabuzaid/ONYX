using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Seo;

namespace Onyx.Infrastructure.Security;

/// <summary>
/// Cloudflare Turnstile siteverify adapter (https://developers.cloudflare.com/turnstile/get-started/server-side-validation/).
/// Accepts a token only when the provider reports success for this secret, the action matches the form, the hostname is one
/// of ours and the challenge is recent. Tokens are single use at the provider. The token, secret and visitor IP are never logged.
/// </summary>
internal sealed partial class TurnstileCaptchaVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    IOptions<PublicSiteOptions> site,
    IOptions<Identity.IdentitySecurityOptions> identity,
    TimeProvider timeProvider,
    ILogger<TurnstileCaptchaVerifier> logger) : ICaptchaVerifier
{
    public const int MaxTokenLength = 2_048;

    private static readonly Error Failed = Error.Validation("Captcha.Failed", "The anti-abuse check failed. Please try again.");
    private static readonly Error Unavailable = Error.Unavailable("Captcha.Unavailable", "The anti-abuse check is not available right now. Please try again later.");

    public bool IsConfigured => options.Value.IsConfigured && AllowedHostnames(options.Value).Count > 0;

    public async Task<Result> VerifyAsync(string? token, string expectedAction, string? remoteIp, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var hostnames = AllowedHostnames(settings);
        if (!settings.IsConfigured || hostnames.Count == 0)
        {
            LogNotConfigured(logger);
            return Unavailable;
        }

        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength)
        {
            return Failed;
        }

        var form = new Dictionary<string, string> { ["secret"] = settings.SecretKey, ["response"] = token };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp;
        }

        SiteVerifyResponse? response;
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var reply = await httpClient.PostAsync(new Uri(settings.VerifyEndpoint), content, cancellationToken).ConfigureAwait(false);
            if (!reply.IsSuccessStatusCode)
            {
                LogProviderStatus(logger, (int)reply.StatusCode);
                return Unavailable;
            }

            response = await reply.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
            && !cancellationToken.IsCancellationRequested)
        {
            LogProviderUnreachable(logger, exception.GetType().Name);
            return Unavailable;
        }

        if (response is not { Success: true })
        {
            LogRejected(logger, string.Join(',', response?.ErrorCodes ?? []));
            return Failed;
        }

        var recent = response.ChallengeTimestamp is { } issued && timeProvider.GetUtcNow() - issued <= TimeSpan.FromSeconds(settings.MaxTokenAgeSeconds);
        var hostnameOk = response.Hostname is { } hostname && hostnames.Contains(hostname);
        if (!string.Equals(response.Action, expectedAction, StringComparison.Ordinal) || !hostnameOk || !recent)
        {
            LogMismatch(logger, expectedAction);
            return Failed;
        }

        return Result.Success();
    }

    private HashSet<string> AllowedHostnames(TurnstileOptions settings)
    {
        var configured = settings.AllowedHostnames.Where(host => !string.IsNullOrWhiteSpace(host)).Select(host => host.Trim().ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        if (configured.Count == 0)
        {
            // The public site (forms) and the admin portal (sign-in, password reset).
            foreach (var url in new[] { site.Value.PublicBaseUrl, identity.Value.AdminPortalBaseUrl })
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var origin))
                {
                    configured.Add(origin.IdnHost.ToLowerInvariant());
                }
            }
        }

        return configured;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile is not configured; protected submissions are refused")]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile verification returned HTTP {StatusCode}")]
    private static partial void LogProviderStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile verification unreachable ({ErrorType})")]
    private static partial void LogProviderUnreachable(ILogger logger, string errorType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Turnstile rejected a token ({ErrorCodes})")]
    private static partial void LogRejected(ILogger logger, string errorCodes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Turnstile token did not match action {ExpectedAction}, hostname or age")]
    private static partial void LogMismatch(ILogger logger, string expectedAction);

    private sealed record SiteVerifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("challenge_ts")] DateTimeOffset? ChallengeTimestamp,
        [property: JsonPropertyName("hostname")] string? Hostname,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("error-codes")] IReadOnlyList<string>? ErrorCodes);
}
