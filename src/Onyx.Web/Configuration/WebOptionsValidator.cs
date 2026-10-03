using Microsoft.Extensions.Options;

namespace Onyx.Web.Configuration;

/// <summary>Startup checks for addresses that must be HTTPS (http only for localhost during development).</summary>
public sealed class WebOptionsValidator : IValidateOptions<OnyxApiOptions>, IValidateOptions<SiteOptions>, IValidateOptions<WebLocalizationOptions>
{
    public ValidateOptionsResult Validate(string? name, OnyxApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsSecureOrigin(options.BaseUrl)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("OnyxApi:BaseUrl must be an absolute https URL (http only for localhost).");
    }

    public ValidateOptionsResult Validate(string? name, SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return IsSecureOrigin(options.PublicBaseUrl)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Site:PublicBaseUrl must be an absolute https URL (http only for localhost).");
    }

    public ValidateOptionsResult Validate(string? name, WebLocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var codes = options.Languages.Select(language => language.Code).ToList();
        if (codes.Count == 0 || !codes.Contains(options.DefaultLanguage, StringComparer.Ordinal))
        {
            return ValidateOptionsResult.Fail("Localization must list its languages, including the default language.");
        }

        return options.Languages.All(language => language.Direction is "rtl" or "ltr" && language.Code.Length == 2 && language.Code.All(char.IsAsciiLetterLower))
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("Each language needs a two-letter lowercase code and a direction of rtl or ltr.");
    }

    private static bool IsSecureOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.Query);
}
