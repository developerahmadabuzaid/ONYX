namespace Onyx.Infrastructure.Email.Templates;

/// <summary>
/// A code-owned email template with native copy per language. Layout and encoding are applied by the renderer;
/// templates return plain text only, never HTML.
/// </summary>
public interface IEmailTemplate
{
    string Key { get; }

    IReadOnlyCollection<string> Languages { get; }

    IReadOnlyCollection<string> RequiredValues { get; }

    EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model);
}
