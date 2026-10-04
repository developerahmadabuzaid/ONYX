using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Cms;

/// <summary>
/// Plain-text editing format for structured body blocks (there is no HTML anywhere in CMS content). Blocks are separated by blank
/// lines:
/// <list type="bullet">
/// <item><c>## Heading</c> (also <c>###</c>, <c>####</c>)</item>
/// <item>lines starting with <c>- </c> (bulleted) or <c>1. </c> (numbered) form a list, one item per line</item>
/// <item><c>![alt text](media:GUID "optional caption")</c> on its own is an image</item>
/// <item><c>[[cta:GUID]]</c> on its own is a call to action</item>
/// <item>anything else is a paragraph (its lines are joined)</item>
/// </list>
/// Inline: <c>**strong**</c>, <c>*emphasis*</c>, <c>[text](content:GUID | /path | https://url)</c>; a backslash escapes a marker.
/// The API validates the result again; this format only makes structured blocks editable in a text area.
/// </summary>
public static partial class BlockTextFormat
{
    private const string EscapableCharacters = "\\*[]!#-.";

    public static BlockTextResult Parse(string? text)
    {
        var blocks = new List<ContentBlockDto>();
        var errors = new List<BlockTextError>();
        foreach (var (lines, firstLine) in SplitBlocks(text))
        {
            var first = lines[0];
            var heading = HeadingPattern().Match(first);
            if (heading.Success && lines.Count == 1)
            {
                blocks.Add(new ContentBlockDto("Heading", Level: heading.Groups[1].Length, Spans: ParseInline(heading.Groups[2].Value, firstLine, errors)));
                continue;
            }

            if (lines.All(line => line.StartsWith("- ", StringComparison.Ordinal)))
            {
                blocks.Add(new ContentBlockDto("List", Ordered: false, Items: lines.Select((line, index) => new ListItemDto(ParseInline(line[2..], firstLine + index, errors))).ToList()));
                continue;
            }

            if (lines.All(line => OrderedItemPattern().IsMatch(line)))
            {
                blocks.Add(new ContentBlockDto("List", Ordered: true, Items: lines.Select((line, index) => new ListItemDto(ParseInline(OrderedItemPattern().Match(line).Groups[1].Value, firstLine + index, errors))).ToList()));
                continue;
            }

            if (lines.Count == 1 && first.StartsWith("![", StringComparison.Ordinal))
            {
                var image = ImagePattern().Match(first);
                if (image.Success && Guid.TryParse(image.Groups[2].Value, out var mediaId))
                {
                    var caption = image.Groups[3].Success ? image.Groups[3].Value : null;
                    blocks.Add(new ContentBlockDto("Image", MediaAssetId: mediaId, Alt: Unescape(image.Groups[1].Value), Caption: caption));
                }
                else
                {
                    errors.Add(new BlockTextError("Cms.Body.InvalidImage", firstLine));
                }

                continue;
            }

            if (lines.Count == 1 && first.StartsWith("[[", StringComparison.Ordinal))
            {
                var cta = CtaPattern().Match(first);
                if (cta.Success && Guid.TryParse(cta.Groups[1].Value, out var ctaId))
                {
                    blocks.Add(new ContentBlockDto("CallToAction", CtaId: ctaId));
                }
                else
                {
                    errors.Add(new BlockTextError("Cms.Body.InvalidCta", firstLine));
                }

                continue;
            }

            if (heading.Success)
            {
                errors.Add(new BlockTextError("Cms.Body.HeadingOneLine", firstLine));
                continue;
            }

            blocks.Add(new ContentBlockDto("Paragraph", Spans: ParseInline(string.Join(' ', lines), firstLine, errors)));
        }

        return new BlockTextResult(blocks, errors);
    }

    public static string Format(IReadOnlyList<ContentBlockDto>? blocks)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var block in blocks)
        {
            switch (block.Type)
            {
                case "Heading":
                    parts.Add(new string('#', Math.Clamp(block.Level ?? 2, 2, 4)) + " " + FormatInline(block.Spans));
                    break;
                case "List":
                    var items = (block.Items ?? []).Select((item, index) => (block.Ordered == true ? $"{(index + 1).ToString(CultureInfo.InvariantCulture)}. " : "- ") + FormatInline(item.Spans));
                    parts.Add(string.Join('\n', items));
                    break;
                case "Image":
                    var caption = string.IsNullOrEmpty(block.Caption) ? string.Empty : $" \"{block.Caption.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
                    parts.Add($"![{Escape(block.Alt ?? string.Empty)}](media:{block.MediaAssetId:D}{caption})");
                    break;
                case "CallToAction":
                    parts.Add($"[[cta:{block.CtaId:D}]]");
                    break;
                default:
                    parts.Add(EscapeLineStart(FormatInline(block.Spans)));
                    break;
            }
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>FAQ text: entries separated by blank lines; the first line starts with <c>Q:</c>, the rest (optionally <c>A:</c>) is the answer.</summary>
    public static (IReadOnlyList<FaqDto> Faqs, IReadOnlyList<BlockTextError> Errors) ParseFaqs(string? text)
    {
        var faqs = new List<FaqDto>();
        var errors = new List<BlockTextError>();
        foreach (var (lines, firstLine) in SplitBlocks(text))
        {
            var question = lines[0];
            if (!question.StartsWith("Q:", StringComparison.OrdinalIgnoreCase) || lines.Count < 2)
            {
                errors.Add(new BlockTextError("Cms.Faq.Invalid", firstLine));
                continue;
            }

            var answer = string.Join(' ', lines.Skip(1)).Trim();
            if (answer.StartsWith("A:", StringComparison.OrdinalIgnoreCase))
            {
                answer = answer[2..].Trim();
            }

            faqs.Add(new FaqDto(question[2..].Trim(), answer));
        }

        return (faqs, errors);
    }

    public static string FormatFaqs(IReadOnlyList<FaqDto>? faqs) =>
        faqs is null ? string.Empty : string.Join("\n\n", faqs.Select(faq => $"Q: {faq.Question}\nA: {faq.Answer}"));

    private static IEnumerable<(List<string> Lines, int FirstLine)> SplitBlocks(string? text)
    {
        var current = new List<string>();
        var start = 0;
        var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
            {
                if (current.Count > 0)
                {
                    yield return (current, start);
                    current = [];
                }

                continue;
            }

            if (current.Count == 0)
            {
                start = index + 1;
            }

            current.Add(line);
        }

        if (current.Count > 0)
        {
            yield return (current, start);
        }
    }

    private static List<TextSpanDto> ParseInline(string text, int line, List<BlockTextError> errors)
    {
        var spans = new List<TextSpanDto>();
        ParseInline(text, null, line, errors, spans);
        return spans;
    }

    private static void ParseInline(string text, string? emphasis, int line, List<BlockTextError> errors, List<TextSpanDto> spans)
    {
        var plain = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == '\\' && index + 1 < text.Length && EscapableCharacters.Contains(text[index + 1], StringComparison.Ordinal))
            {
                plain.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (emphasis is null && character == '*')
            {
                var marker = index + 1 < text.Length && text[index + 1] == '*' ? "**" : "*";
                var close = FindUnescaped(text, marker, index + marker.Length);
                if (close > index + marker.Length)
                {
                    Flush(plain, emphasis, spans);
                    ParseInline(text[(index + marker.Length)..close], marker == "**" ? "Strong" : "Emphasis", line, errors, spans);
                    index = close + marker.Length;
                    continue;
                }
            }

            if (character == '[')
            {
                var link = LinkPattern().Match(text, index);
                if (link.Success && link.Index == index)
                {
                    var target = ParseTarget(link.Groups[2].Value.Trim());
                    if (target is null)
                    {
                        errors.Add(new BlockTextError("Cms.Body.InvalidLink", line));
                    }
                    else
                    {
                        Flush(plain, emphasis, spans);
                        spans.Add(new TextSpanDto(Unescape(link.Groups[1].Value), emphasis, target));
                    }

                    index += link.Length;
                    continue;
                }
            }

            plain.Append(character);
            index++;
        }

        Flush(plain, emphasis, spans);
    }

    private static LinkTargetDto? ParseTarget(string target)
    {
        if (target.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
        {
            return Guid.TryParse(target["content:".Length..], out var id) ? new LinkTargetDto(ContentItemId: id) : null;
        }

        if (target.StartsWith('/') && !target.StartsWith("//", StringComparison.Ordinal))
        {
            return new LinkTargetDto(Path: target);
        }

        return target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? new LinkTargetDto(Url: target) : null;
    }

    private static string FormatInline(IReadOnlyList<TextSpanDto>? spans)
    {
        var builder = new StringBuilder();
        foreach (var span in spans ?? [])
        {
            var text = Escape(span.Text);
            if (span.Link is { } link)
            {
                var target = link.ContentItemId is { } id ? $"content:{id:D}" : link.Path ?? link.Url ?? string.Empty;
                text = $"[{text}]({target})";
            }

            builder.Append(span.Emphasis switch
            {
                "Strong" => $"**{text}**",
                "Emphasis" => $"*{text}*",
                _ => text,
            });
        }

        return builder.ToString();
    }

    private static void Flush(StringBuilder plain, string? emphasis, List<TextSpanDto> spans)
    {
        if (plain.Length > 0)
        {
            spans.Add(new TextSpanDto(plain.ToString(), emphasis));
            plain.Clear();
        }
    }

    private static int FindUnescaped(string text, string marker, int from)
    {
        for (var index = from; index <= text.Length - marker.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (string.CompareOrdinal(text, index, marker, 0, marker.Length) == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character is '\\' or '*' or '[' or ']')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>A paragraph must not be re-read as a heading, list, image or call to action.</summary>
    private static string EscapeLineStart(string text) =>
        text.Length > 0 && (text[0] is '#' or '-' or '!' || OrderedItemPattern().IsMatch(text))
            ? (OrderedItemPattern().IsMatch(text) ? EscapeOrdered(text) : "\\" + text)
            : text;

    private static string EscapeOrdered(string text)
    {
        var dot = text.IndexOf('.', StringComparison.Ordinal);
        return text[..dot] + "\\" + text[dot..];
    }

    private static string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\\' && index + 1 < text.Length && EscapableCharacters.Contains(text[index + 1], StringComparison.Ordinal))
            {
                index++;
            }

            builder.Append(text[index]);
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"^(#{2,4})\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\d{1,3}\.\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedItemPattern();

    [GeneratedRegex(@"^!\[((?:\\.|[^\]\\])*)\]\(media:([0-9a-fA-F-]{36})(?:\s+""([^""]*)"")?\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ImagePattern();

    [GeneratedRegex(@"^\[\[cta:([0-9a-fA-F-]{36})\]\]$", RegexOptions.CultureInvariant)]
    private static partial Regex CtaPattern();

    [GeneratedRegex(@"\[((?:\\.|[^\]\\])+)\]\(([^)\s]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}
