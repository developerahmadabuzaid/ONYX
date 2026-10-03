namespace Onyx.Web.Configuration;

/// <summary>Bound from <c>Localization</c>. Arabic is the default; adding a language is configuration plus resources, not code.</summary>
public sealed class WebLocalizationOptions
{
    public const string SectionName = "Localization";

    public string DefaultLanguage { get; set; } = "ar";

    public IList<LanguageDefinition> Languages { get; } = [];
}
