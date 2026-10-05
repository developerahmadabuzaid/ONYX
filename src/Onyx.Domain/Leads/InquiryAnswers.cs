using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Leads;

/// <summary>Structured form answers (e.g. the project wizard), versioned so their meaning survives later form changes.</summary>
public sealed record InquiryAnswers(int Version, IReadOnlyDictionary<string, IReadOnlyList<string>> Values)
{
    public const int MaxKeys = 20;
    public const int MaxValuesPerKey = 10;
    public const int ValueMaxLength = 200;

    public static InquiryAnswers Empty { get; } = new(1, new Dictionary<string, IReadOnlyList<string>>());

    public static Result<InquiryAnswers> Create(int version, IReadOnlyDictionary<string, IReadOnlyList<string>>? values)
    {
        var source = values ?? new Dictionary<string, IReadOnlyList<string>>();
        if (version < 1 || source.Count > MaxKeys)
        {
            return Error.Validation("Inquiry.InvalidAnswers", "The form answers are not valid.");
        }

        var cleaned = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, list) in source)
        {
            if (key.Length is 0 or > 50 || !key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
            {
                return Error.Validation("Inquiry.InvalidAnswers", "The form answers are not valid.");
            }

            var items = (list ?? []).Select(item => item?.Trim() ?? string.Empty).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToList();
            if (items.Count > MaxValuesPerKey || items.Any(item => item.Length > ValueMaxLength || ContentBodyRules.HasControlCharacters(item, allowNewLines: false)))
            {
                return Error.Validation("Inquiry.InvalidAnswers", "The form answers are not valid.");
            }

            if (items.Count > 0)
            {
                cleaned[key] = items;
            }
        }

        return new InquiryAnswers(version, cleaned);
    }
}
