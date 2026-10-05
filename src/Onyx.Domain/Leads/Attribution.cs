namespace Onyx.Domain.Leads;

/// <summary>
/// Where an inquiry came from (architecture.md §13.2, §14). Consent-aware: without analytics consent only the submitting
/// session's own context is kept (its touch, the submission page, language, market and device class); the persistent
/// first-touch record and the analytics client id are kept only when the visitor allowed analytics.
/// </summary>
public sealed record Attribution(
    TouchPoint? FirstTouch,
    TouchPoint? LastTouch,
    string? SubmissionPath,
    string LanguageCode,
    string? Market,
    string? DeviceClass,
    string? ToolSource,
    Guid? ToolRunId,
    string? AnalyticsClientId,
    bool AnalyticsConsent)
{
    public const int ClientIdMaxLength = 100;

    private static readonly HashSet<string> DeviceClasses = new(StringComparer.Ordinal) { "desktop", "mobile", "tablet" };

    public static Attribution Create(
        TouchPoint? firstTouch, TouchPoint? lastTouch, string? submissionPath, string languageCode, string? market, string? deviceClass,
        string? analyticsClientId, bool analyticsConsent)
    {
        var device = deviceClass?.Trim().ToLowerInvariant();
        var country = market?.Trim().ToUpperInvariant();
        var clientId = analyticsClientId?.Trim();
        return new Attribution(
            analyticsConsent ? TouchPoint.Clean(firstTouch) : null,
            TouchPoint.Clean(lastTouch),
            TouchPoint.Path(submissionPath),
            languageCode,
            country is { Length: 2 } && country.All(char.IsAsciiLetterUpper) ? country : null,
            device is not null && DeviceClasses.Contains(device) ? device : null,
            null,
            null,
            analyticsConsent && clientId is { Length: > 0 and <= ClientIdMaxLength } && clientId.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-')
                ? clientId
                : null,
            analyticsConsent);
    }
}
