namespace Onyx.Web.Configuration;

public sealed class LanguageDefinition
{
    public string Code { get; set; } = string.Empty;

    /// <summary><c>rtl</c> or <c>ltr</c>.</summary>
    public string Direction { get; set; } = "ltr";

    /// <summary>The language's own name, shown in the language switcher.</summary>
    public string NativeName { get; set; } = string.Empty;
}
