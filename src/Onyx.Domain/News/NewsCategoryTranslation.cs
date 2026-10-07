using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>A category's name and URL slug in one language.</summary>
public sealed class NewsCategoryTranslation : Entity, IAuditable
{
    public const int NameMaxLength = 80;

    private NewsCategoryTranslation()
    {
        LanguageCode = string.Empty;
        Name = string.Empty;
        Slug = string.Empty;
    }

    internal NewsCategoryTranslation(Guid newsCategoryId, string languageCode, string name, string slug)
    {
        NewsCategoryId = newsCategoryId;
        LanguageCode = languageCode;
        Name = name;
        Slug = slug;
    }

    public Guid NewsCategoryId { get; private set; }

    public string LanguageCode { get; private set; }

    public string Name { get; private set; }

    public string Slug { get; private set; }

    internal void Change(string name, string slug)
    {
        Name = name;
        Slug = slug;
    }
}
