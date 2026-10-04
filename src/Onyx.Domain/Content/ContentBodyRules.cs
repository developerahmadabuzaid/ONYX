using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>Structural and size rules for bodies and FAQs. Bounded so a single request cannot store unbounded data.</summary>
public static class ContentBodyRules
{
    public const int MaxBlocks = 400;
    public const int MaxSpansPerBlock = 200;
    public const int MaxListItems = 100;
    public const int MaxSpanLength = 5_000;
    public const int MaxTotalTextLength = 200_000;
    public const int MaxAltLength = 300;
    public const int MaxCaptionLength = 500;
    public const int MaxPathLength = 500;
    public const int MaxUrlLength = 2_000;
    public const int MaxFaqs = 50;
    public const int MaxQuestionLength = 300;
    public const int MaxAnswerLength = 4_000;

    public static Error? Validate(IReadOnlyList<ContentBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Count > MaxBlocks)
        {
            return Invalid($"A body may contain at most {MaxBlocks} blocks.");
        }

        var totalText = 0;
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (block is null)
            {
                return Invalid($"Block {index + 1} is empty.");
            }

            var problem = block.Type switch
            {
                ContentBlockType.Heading => ValidateHeading(block, ref totalText),
                ContentBlockType.Paragraph => ValidateParagraph(block, ref totalText),
                ContentBlockType.List => ValidateList(block, ref totalText),
                ContentBlockType.Image => ValidateImage(block),
                ContentBlockType.CallToAction => block.CtaId is { } id && id != Guid.Empty && OnlyFields(block, cta: true)
                    ? null
                    : "A call-to-action block needs a call to action and nothing else.",
                _ => "Unknown block type.",
            };

            if (problem is not null)
            {
                return Invalid($"Block {index + 1}: {problem}");
            }

            if (totalText > MaxTotalTextLength)
            {
                return Invalid($"The body may contain at most {MaxTotalTextLength} characters of text.");
            }
        }

        return null;
    }

    public static Error? ValidateFaqs(IReadOnlyList<FaqEntry> faqs)
    {
        ArgumentNullException.ThrowIfNull(faqs);
        if (faqs.Count > MaxFaqs)
        {
            return Invalid($"At most {MaxFaqs} FAQs are allowed.");
        }

        foreach (var faq in faqs)
        {
            if (faq is null || string.IsNullOrWhiteSpace(faq.Question) || string.IsNullOrWhiteSpace(faq.Answer))
            {
                return Invalid("Every FAQ needs a question and an answer.");
            }

            if (faq.Question.Length > MaxQuestionLength || faq.Answer.Length > MaxAnswerLength
                || HasControlCharacters(faq.Question, allowNewLines: false) || HasControlCharacters(faq.Answer, allowNewLines: true))
            {
                return Invalid($"FAQ questions are limited to {MaxQuestionLength} and answers to {MaxAnswerLength} characters of plain text.");
            }
        }

        return null;
    }

    /// <summary>
    /// A site-relative path such as <c>/ar/الخدمات</c>: no scheme, no protocol-relative <c>//</c>, no backslashes and no dot segments
    /// (a link can never climb to another area such as <c>/admin</c>).
    /// </summary>
    public static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrEmpty(path) && path.Length <= MaxPathLength && path[0] == '/' && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Contains('\\', StringComparison.Ordinal) && !HasControlCharacters(path, allowNewLines: false) && !path.Contains(' ', StringComparison.Ordinal)
        && !path.Split('/', '?', '#').Any(segment => segment is "." or "..");

    public static bool IsSafeExternalUrl(string? url) =>
        !string.IsNullOrEmpty(url) && url.Length <= MaxUrlLength && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);

    public static bool HasControlCharacters(string value, bool allowNewLines)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (var character in value)
        {
            if (char.IsControl(character) && !(allowNewLines && character == '\n'))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ValidateHeading(ContentBlock block, ref int totalText)
    {
        if (block.Level is not (2 or 3 or 4))
        {
            return "Headings must be level 2, 3 or 4 (the title is the page's only H1).";
        }

        return OnlyFields(block, level: true, spans: true) ? ValidateSpans(block.Spans, ref totalText, allowLinks: false) : "Unexpected fields for a heading.";
    }

    private static string? ValidateParagraph(ContentBlock block, ref int totalText) =>
        OnlyFields(block, spans: true) ? ValidateSpans(block.Spans, ref totalText, allowLinks: true) : "Unexpected fields for a paragraph.";

    private static string? ValidateList(ContentBlock block, ref int totalText)
    {
        if (!OnlyFields(block, items: true, ordered: true) || block.Items is not { Count: > 0 } items || items.Count > MaxListItems)
        {
            return $"A list needs 1 to {MaxListItems} items.";
        }

        foreach (var item in items)
        {
            if (item is null || ValidateSpans(item.Spans, ref totalText, allowLinks: true) is { } problem)
            {
                return "Every list item needs text.";
            }
        }

        return null;
    }

    private static string? ValidateImage(ContentBlock block)
    {
        if (!OnlyFields(block, media: true, alt: true, caption: true) || block.MediaAssetId is not { } id || id == Guid.Empty)
        {
            return "An image block needs a media library image.";
        }

        if (block.Alt is { } alt && (alt.Length > MaxAltLength || HasControlCharacters(alt, allowNewLines: false)))
        {
            return $"Alt text is limited to {MaxAltLength} characters of plain text.";
        }

        return block.Caption is { } caption && (caption.Length > MaxCaptionLength || HasControlCharacters(caption, allowNewLines: false))
            ? $"Captions are limited to {MaxCaptionLength} characters of plain text."
            : null;
    }

    private static string? ValidateSpans(IReadOnlyList<TextSpan>? spans, ref int totalText, bool allowLinks)
    {
        if (spans is not { Count: > 0 } || spans.Count > MaxSpansPerBlock)
        {
            return $"Text blocks need 1 to {MaxSpansPerBlock} text runs.";
        }

        var hasText = false;
        foreach (var span in spans)
        {
            if (span?.Text is null || span.Text.Length > MaxSpanLength || HasControlCharacters(span.Text, allowNewLines: false))
            {
                return $"Text runs are limited to {MaxSpanLength} characters of plain text.";
            }

            hasText |= !string.IsNullOrWhiteSpace(span.Text);
            totalText += span.Text.Length;

            if (span.Link is { } link)
            {
                if (!allowLinks)
                {
                    return "Headings cannot contain links.";
                }

                if (ValidateLink(link) is { } problem)
                {
                    return problem;
                }
            }
        }

        return hasText ? null : "Text blocks cannot be blank.";
    }

    private static string? ValidateLink(LinkTarget link)
    {
        var targets = (link.ContentItemId is not null ? 1 : 0) + (link.Path is not null ? 1 : 0) + (link.Url is not null ? 1 : 0);
        if (targets != 1)
        {
            return "A link needs exactly one target: a content item, a site path or an https URL.";
        }

        if (link.ContentItemId == Guid.Empty)
        {
            return "Linked content is missing.";
        }

        if (link.Path is not null && !IsSafeRelativePath(link.Path))
        {
            return "Site links must be paths starting with a single '/'.";
        }

        return link.Url is not null && !IsSafeExternalUrl(link.Url) ? "External links must be absolute https URLs." : null;
    }

    private static bool OnlyFields(
        ContentBlock block,
        bool level = false,
        bool spans = false,
        bool items = false,
        bool ordered = false,
        bool media = false,
        bool alt = false,
        bool caption = false,
        bool cta = false) =>
        (level || block.Level is null) && (spans || block.Spans is null) && (items || block.Items is null) && (ordered || block.Ordered is null)
        && (media || block.MediaAssetId is null) && (alt || block.Alt is null) && (caption || block.Caption is null) && (cta || block.CtaId is null);

    private static Error Invalid(string message) => Error.Validation("Content.InvalidBody", message);
}
