using System.Text;
using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Application.Email;

/// <summary>
/// Placeholder rules for admin-edited email wording: only the template's declared <c>{placeholders}</c> may appear, required
/// ones must appear, and stray braces are rejected. Values are inserted as plain text and HTML-encoded by the renderer.
/// </summary>
public static partial class EmailCopyRules
{
    public static Error? Validate(EmailCopy copy, EmailTemplateDescriptor template)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(template);
        var texts = new[] { copy.Subject, copy.Heading, copy.ActionLabel, copy.Footer }.Concat(copy.Paragraphs).Where(text => text is not null).Cast<string>().ToList();

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var text in texts)
        {
            var withoutTokens = Token().Replace(text, match =>
            {
                used.Add(match.Groups[1].Value);
                return string.Empty;
            });

            if (withoutTokens.Contains('{', StringComparison.Ordinal) || withoutTokens.Contains('}', StringComparison.Ordinal))
            {
                return Invalid("Use braces only for placeholders such as {name}.");
            }
        }

        if (used.FirstOrDefault(token => !template.Placeholders.Contains(token)) is { } unknown)
        {
            return Invalid($"'{{{unknown}}}' is not available in this email. Allowed: {Format(template.Placeholders)}.");
        }

        if (template.RequiredPlaceholders.FirstOrDefault(token => !used.Contains(token)) is { } missing)
        {
            return Invalid($"This email must mention {{{missing}}}.");
        }

        if (template.HasAction ? string.IsNullOrWhiteSpace(copy.ActionLabel) : copy.ActionLabel is not null)
        {
            return Invalid(template.HasAction ? "This email has a button and needs its label." : "This email has no button.");
        }

        return null;
    }

    /// <summary>Replaces <c>{placeholder}</c> tokens with model values (unknown tokens are left as they are).</summary>
    public static string Fill(string text, IReadOnlyDictionary<string, string> model)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(model);
        return Token().Replace(text, match => model.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
    }

    private static string Format(IReadOnlyCollection<string> placeholders) =>
        placeholders.Count == 0 ? "none" : new StringBuilder().AppendJoin(", ", placeholders.Select(token => $"{{{token}}}")).ToString();

    private static Error Invalid(string message) => Error.Validation("EmailTemplate.InvalidPlaceholders", message);

    [GeneratedRegex(@"\{([a-zA-Z][a-zA-Z0-9]{0,30})\}", RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
