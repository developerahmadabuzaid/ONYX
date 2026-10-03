using Microsoft.Extensions.Options;
using Onyx.Web.Configuration;

namespace Onyx.Web.Localization;

/// <summary>Configured languages, their direction and the default (Arabic). The single source for language decisions in Onyx.Web.</summary>
public sealed class LanguageCatalog(IOptions<WebLocalizationOptions> options)
{
    public const string ChoiceCookieName = "onyx.lang";

    public string DefaultLanguage => options.Value.DefaultLanguage;

    public IReadOnlyList<LanguageDefinition> Languages => options.Value.Languages.ToList();

    public bool IsSupported(string? code) => code is not null && options.Value.Languages.Any(language => language.Code == code);

    public string Direction(string code) => options.Value.Languages.FirstOrDefault(language => language.Code == code)?.Direction ?? "ltr";

    public string NativeName(string code) => options.Value.Languages.FirstOrDefault(language => language.Code == code)?.NativeName ?? code;
}
