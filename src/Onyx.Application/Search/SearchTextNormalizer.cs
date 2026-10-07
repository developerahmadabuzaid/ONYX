using System.Globalization;
using System.Text;

namespace Onyx.Application.Search;

/// <summary>
/// The one shared normalizer for site search (architecture.md §18), applied identically when indexing and when querying:
/// Unicode NFKC; Arabic tashkeel and tatweel removed; alef forms (أ إ آ ٱ) → ا, ة → ه, ى → ي, ؤ → و, ئ → ي; Arabic-Indic and
/// Persian digits → Western digits; punctuation and symbols → spaces; lower case; whitespace collapsed. English text is thereby
/// lower-cased and NFKC-normalized. The original text is kept separately for display.
/// </summary>
public static class SearchTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var composed = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(composed.Length);
        var pendingSpace = false;
        foreach (var character in composed)
        {
            var mapped = Map(character);
            if (mapped is null)
            {
                continue;
            }

            if (mapped == ' ')
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(mapped.Value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Text for the stemmed index: <see cref="Normalize"/>, plus the <see cref="BareForm"/> of every word that has one, appended
    /// after the original text so phrase positions are unchanged. Queries use the bare form (<see cref="ParseQuery"/>), so the
    /// same word matches with or without the article or conjunction.
    /// </summary>
    public static string ForIndex(string? text)
    {
        var normalized = Normalize(text);
        var extra = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(BareForm).OfType<string>().ToList();
        return extra.Count == 0 ? normalized : $"{normalized} {string.Join(' ', extra)}";
    }

    /// <summary>
    /// A normalized Arabic word without a leading conjunction (و or ف directly before ال) and the article ال, or null when the
    /// word has neither. PostgreSQL's Snowball Arabic stemmer does not strip a conjunction before the article (والذكاء stays
    /// whole) and, once it strips the article, no longer strips a final ه (normalized ة) either: الموسسه stems to موسسه while
    /// موسسه stems to موسس. Bare forms make index and query stem alike. Short words (a bare form under four letters) are kept,
    /// so words that merely begin with these letters are left alone.
    /// </summary>
    public static string? BareForm(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var start = word.Length >= 5 && (word[0] == 'و' || word[0] == 'ف') && word[1] == 'ا' && word[2] == 'ل' ? 1 : 0;
        if (word.Length - start >= 6 && word[start] == 'ا' && word[start + 1] == 'ل')
        {
            return word[(start + 2)..];
        }

        return start == 1 ? word[1..] : null;
    }

    /// <summary>
    /// Parses a visitor query into normalized words. Only the web-search syntax PostgreSQL parses safely is kept: double-quoted
    /// phrases and a leading minus that excludes a word or phrase. All other punctuation is dropped. The web query uses bare
    /// forms (<see cref="BareForm"/>) to match the stemmed index.
    /// </summary>
    public static ParsedSearchQuery ParseQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new ParsedSearchQuery(string.Empty, string.Empty, string.Empty);
        }

        var included = new List<string>();
        var excluded = new List<string>();
        var web = new List<string>();
        var inPhrase = false;
        var phraseExcluded = false;
        foreach (var token in query.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var continuesPhrase = inPhrase;
            var exclude = continuesPhrase ? phraseExcluded : token.Length > 1 && token[0] == '-';
            var opensPhrase = !continuesPhrase && token.TrimStart('-').StartsWith('"');
            var closesPhrase = (continuesPhrase || opensPhrase) && token.Length > 1 && token.EndsWith('"');
            if (opensPhrase && !closesPhrase)
            {
                inPhrase = true;
                phraseExcluded = exclude;
            }
            else if (closesPhrase)
            {
                inPhrase = false;
            }

            var words = Normalize(token);
            if (words.Length == 0)
            {
                continue;
            }

            var bare = string.Join(' ', words.Split(' ').Select(word => BareForm(word) ?? word));
            if (exclude)
            {
                // Both forms: the original for the unstemmed names vector, the bare form for the stemmed vector.
                excluded.AddRange(words.Split(' ').Concat(bare.Split(' ')).Distinct(StringComparer.Ordinal));
            }
            else
            {
                included.Add(words);
            }

            web.Add($"{(exclude && !continuesPhrase ? "-" : string.Empty)}{(opensPhrase ? "\"" : string.Empty)}{bare}{(closesPhrase ? "\"" : string.Empty)}");
        }

        return new ParsedSearchQuery(string.Join(' ', included), string.Join(' ', web), string.Join(' ', excluded));
    }

    /// <summary>Null drops the character; a space separates words.</summary>
    private static char? Map(char character)
    {
        switch (character)
        {
            // Tatweel and Arabic diacritics (harakat, tanwin, shadda, sukun, superscript alef, Quranic marks).
            case 'ـ':
            case >= 'ً' and <= 'ٟ':
            case 'ٰ':
            case >= 'ۖ' and <= 'ۭ':
                return null;

            case 'أ' or 'إ' or 'آ' or 'ٱ':
                return 'ا';
            case 'ة':
                return 'ه';
            case 'ى':
                return 'ي';
            case 'ؤ':
                return 'و';
            case 'ئ':
                return 'ي';

            // Arabic-Indic and Extended Arabic-Indic (Persian) digits.
            case >= '٠' and <= '٩':
                return (char)('0' + (character - '٠'));
            case >= '۰' and <= '۹':
                return (char)('0' + (character - '۰'));
        }

        if (char.IsWhiteSpace(character) || char.IsControl(character))
        {
            return ' ';
        }

        var category = char.GetUnicodeCategory(character);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.Format)
        {
            return null;
        }

        return char.IsLetterOrDigit(character) ? character : ' ';
    }
}
