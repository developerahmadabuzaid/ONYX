using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Messaging;

/// <summary>
/// Admin-edited wording for one language of a code-owned email template. Plain text with <c>{placeholder}</c> tokens only:
/// layout, encoding and links (including reset/invitation URLs) stay code-owned and cannot be changed here.
/// </summary>
public sealed class EmailTemplateCopy : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 100;
    public const int SubjectMaxLength = 200;
    public const int HeadingMaxLength = 200;
    public const int ParagraphMaxLength = 1_000;
    public const int MaxParagraphs = 10;
    public const int ActionLabelMaxLength = 100;
    public const int FooterMaxLength = 500;

    private EmailTemplateCopy()
    {
        TemplateKey = string.Empty;
        LanguageCode = string.Empty;
        Subject = string.Empty;
        Heading = string.Empty;
        Paragraphs = [];
    }

    public string TemplateKey { get; private set; }

    public string LanguageCode { get; private set; }

    public string Subject { get; private set; }

    public string Heading { get; private set; }

    public IReadOnlyList<string> Paragraphs { get; private set; }

    public string? ActionLabel { get; private set; }

    public string? Footer { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.System;

    public static EmailTemplateCopy Create(string templateKey, string languageCode) =>
        new() { TemplateKey = templateKey, LanguageCode = languageCode };

    /// <summary>Lengths only; placeholder rules are checked against the code template by the application layer.</summary>
    public Result Update(string subject, string heading, IReadOnlyList<string> paragraphs, string? actionLabel, string? footer)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        static bool Bad(string? value, int max, bool required) =>
            (required && string.IsNullOrWhiteSpace(value)) || value is not null && (value.Length > max || value.Any(character => char.IsControl(character)));

        if (Bad(subject, SubjectMaxLength, true) || Bad(heading, HeadingMaxLength, true) || Bad(actionLabel, ActionLabelMaxLength, false)
            || Bad(footer, FooterMaxLength, false) || paragraphs.Count is 0 or > MaxParagraphs || paragraphs.Any(paragraph => Bad(paragraph, ParagraphMaxLength, true)))
        {
            return Error.Validation("EmailTemplate.InvalidCopy", "Subject, heading and 1-10 paragraphs of plain text are required, within their length limits.");
        }

        Subject = subject.Trim();
        Heading = heading.Trim();
        Paragraphs = paragraphs.Select(paragraph => paragraph.Trim()).ToList();
        ActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? null : actionLabel.Trim();
        Footer = string.IsNullOrWhiteSpace(footer) ? null : footer.Trim();
        return Result.Success();
    }
}
