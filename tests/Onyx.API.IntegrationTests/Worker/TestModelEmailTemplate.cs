using Onyx.Infrastructure.Email.Templates;

namespace Onyx.API.IntegrationTests.Worker;

/// <summary>Test-only template that echoes a model value, to prove model values are HTML-encoded.</summary>
public sealed class TestModelEmailTemplate : IEmailTemplate
{
    public const string TemplateKey = "test.model";

    public string Key => TemplateKey;

    public IReadOnlyCollection<string> Languages { get; } = ["ar", "en"];

    public IReadOnlyCollection<string> RequiredValues { get; } = ["name"];

    public EmailContent Compose(string languageCode, IReadOnlyDictionary<string, string> model) =>
        new(
            Subject: languageCode == "ar" ? $"مرحباً {model["name"]}" : $"Hello {model["name"]}",
            Heading: model["name"],
            Paragraphs: [model["name"]],
            Action: model.TryGetValue("link", out var link) ? new EmailAction("Open", new Uri(link)) : null,
            Footer: null);
}
