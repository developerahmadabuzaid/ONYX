using System.Text;

namespace Onyx.Domain.Content;

/// <summary>
/// Localized slug rules (docs/architecture.md §22): NFC; tashkeel and tatweel stripped; Eastern Arabic digits converted to
/// Western digits; only Arabic letters, a–z, 0–9 and single hyphens. Stored as Unicode text and percent-encoded when emitted.
/// </summary>
public static class Slug
{
    public const int MaxLength = 80;
    public const int RecommendedMaxLength = 60;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingHyphen = false;
        foreach (var raw in value.Normalize(NormalizationForm.FormC))
        {
            var character = ToWesternDigit(raw);
            if (TextNormalization.IsArabicDiacriticOrTatweel(character))
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z')
            {
                character = char.ToLowerInvariant(character);
            }

            if (character is >= 'a' and <= 'z' or >= '0' and <= '9' || IsArabicLetter(character))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(character);
            }
            else
            {
                // Spaces, punctuation, hyphens and anything else become a single separator.
                pendingHyphen = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>True when <paramref name="value"/> is already a normalized, non-empty slug within the length limit.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= MaxLength && string.Equals(Normalize(value), value, StringComparison.Ordinal);

    public static bool IsArabicLetter(char character) =>
        (int)character is (>= 0x0621 and <= 0x063A) or (>= 0x0641 and <= 0x064A) or 0x0671 or 0x067E or 0x0686 or 0x0698 or 0x06A9 or 0x06AF or 0x06CC;

    private static char ToWesternDigit(char character) => character switch
    {
        >= (char)0x0660 and <= (char)0x0669 => (char)('0' + (character - 0x0660)),
        >= (char)0x06F0 and <= (char)0x06F9 => (char)('0' + (character - 0x06F0)),
        _ => character,
    };
}
