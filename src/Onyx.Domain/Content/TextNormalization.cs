using System.Text;

namespace Onyx.Domain.Content;

/// <summary>
/// Comparison form for keywords (cannibalization checks, keyword map uniqueness): NFC, lower case, tashkeel/tatweel removed,
/// whitespace collapsed. Display text is always kept as authored. Full search normalization belongs to site search (Stage 10).
/// </summary>
public static class TextNormalization
{
    public static string NormalizeKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Normalize(NormalizationForm.FormC))
        {
            if (IsArabicDiacriticOrTatweel(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    public static bool IsArabicDiacriticOrTatweel(char character) =>
        (int)character is (>= 0x064B and <= 0x065F) or 0x0670 or (>= 0x06D6 and <= 0x06ED) or 0x0640;
}
