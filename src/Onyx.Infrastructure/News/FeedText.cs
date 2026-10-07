using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.News;

/// <summary>Plain text and dates from untrusted feed markup. Output is only ever used as text (encoded when shown), never as HTML.</summary>
public static partial class FeedText
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly Dictionary<string, string> NamedZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GMT"] = "+00:00", ["UT"] = "+00:00", ["UTC"] = "+00:00", ["Z"] = "+00:00",
        ["EST"] = "-05:00", ["EDT"] = "-04:00", ["CST"] = "-06:00", ["CDT"] = "-05:00",
        ["MST"] = "-07:00", ["MDT"] = "-06:00", ["PST"] = "-08:00", ["PDT"] = "-07:00",
    };

    /// <summary>Tags removed, entities decoded, whitespace collapsed, at most <paramref name="maxLength"/> characters; null when empty.</summary>
    public static string? ToPlainText(string? markup, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return null;
        }

        var bounded = markup.Length > maxLength * 4 ? markup[..(maxLength * 4)] : markup;
        string withoutTags;
        try
        {
            withoutTags = TagPattern().Replace(bounded, " ");
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        var text = NewsItem.CleanText(WebUtility.HtmlDecode(withoutTags).Replace('<', ' ').Replace('>', ' '));
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    /// <summary>RFC 822/1123 (as in RSS) and ISO 8601 (as in Atom) dates; null when the text is not a usable date.</summary>
    public static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        var zone = ZonePattern().Match(text);
        if (zone.Success)
        {
            var name = zone.Groups["zone"].Value;
            var offset = NamedZones.TryGetValue(name, out var named)
                ? named
                : name.Length == 5 && name[0] is '+' or '-' ? $"{name[..3]}:{name[3..]}" : null;
            if (offset is not null)
            {
                text = $"{text[..zone.Index]} {offset}";
            }
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    [GeneratedRegex("<[^>]{0,2000}>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s(?<zone>[A-Za-z]{1,3}|[+-]\d{4})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex ZonePattern();
}
