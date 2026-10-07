using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>
/// A News Hub category (architecture.md §17.3: four at launch, admin-extensible). Keywords drive the rule-based classifier; they
/// are stored already normalized (the caller applies the shared search normalizer).
/// </summary>
public sealed class NewsCategory : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 60;
    public const int KeywordMaxLength = 60;
    public const int MaxKeywords = 100;

    private readonly List<NewsCategoryTranslation> _translations = [];

    private NewsCategory()
    {
        Key = string.Empty;
        Keywords = [];
    }

    /// <summary>Stable identifier (lower-case letters, digits and hyphens); never shown publicly.</summary>
    public string Key { get; private set; }

    public int SortOrder { get; private set; }

    public List<string> Keywords { get; private set; }

    public IReadOnlyCollection<NewsCategoryTranslation> Translations => _translations.AsReadOnly();

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public static Result<NewsCategory> Create(string? key, int sortOrder)
    {
        var cleanKey = key?.Trim();
        if (string.IsNullOrEmpty(cleanKey) || cleanKey.Length > KeyMaxLength || !string.Equals(Content.Slug.Normalize(cleanKey), cleanKey, StringComparison.Ordinal)
            || !cleanKey.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
        {
            return Error.Validation("News.InvalidCategoryKey", "The key uses lower-case letters, digits and hyphens.");
        }

        return new NewsCategory { Key = cleanKey, SortOrder = sortOrder };
    }

    public NewsCategoryTranslation? Translation(string languageCode) =>
        _translations.FirstOrDefault(translation => translation.LanguageCode == languageCode);

    /// <summary>Sets the name and slug in one language. The caller ensures the slug is unique among categories and items.</summary>
    public Result SetTranslation(string languageCode, string? name, string? slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var cleanName = name?.Trim();
        if (string.IsNullOrEmpty(cleanName) || cleanName.Length > NewsCategoryTranslation.NameMaxLength)
        {
            return Error.Validation("News.InvalidCategoryName", $"Enter the category name (at most {NewsCategoryTranslation.NameMaxLength} characters).");
        }

        var cleanSlug = Content.Slug.Normalize(string.IsNullOrWhiteSpace(slug) ? cleanName : slug);
        if (!Content.Slug.IsValid(cleanSlug))
        {
            return Error.Validation("News.InvalidCategorySlug", "The category needs a valid slug.");
        }

        if (Translation(languageCode) is { } existing)
        {
            existing.Change(cleanName, cleanSlug);
        }
        else
        {
            _translations.Add(new NewsCategoryTranslation(Id, languageCode, cleanName, cleanSlug));
        }

        return Result.Success();
    }

    public Result SetKeywords(IEnumerable<string> normalizedKeywords)
    {
        ArgumentNullException.ThrowIfNull(normalizedKeywords);
        var keywords = normalizedKeywords.Select(keyword => keyword.Trim()).Where(keyword => keyword.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (keywords.Count > MaxKeywords || keywords.Any(keyword => keyword.Length > KeywordMaxLength))
        {
            return Error.Validation("News.InvalidKeywords", $"At most {MaxKeywords} keywords of up to {KeywordMaxLength} characters.");
        }

        Keywords = keywords;
        return Result.Success();
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
