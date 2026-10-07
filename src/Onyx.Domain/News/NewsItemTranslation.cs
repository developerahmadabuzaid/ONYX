using Onyx.Domain.Common;

namespace Onyx.Domain.News;

/// <summary>Onyx's own headline and summary of an item in one language, and its URL slug (fixed once the item has been public).</summary>
public sealed class NewsItemTranslation : Entity, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int SummaryMinLength = 40;
    public const int SummaryMaxLength = 700;

    private NewsItemTranslation()
    {
        LanguageCode = string.Empty;
        Title = string.Empty;
        Summary = string.Empty;
        Slug = string.Empty;
    }

    internal NewsItemTranslation(Guid newsItemId, string languageCode, string title, string summary, string slug)
    {
        NewsItemId = newsItemId;
        LanguageCode = languageCode;
        Title = title;
        Summary = summary;
        Slug = slug;
    }

    public Guid NewsItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public string Title { get; private set; }

    public string Summary { get; private set; }

    public string Slug { get; private set; }

    internal void Change(string title, string summary, string? slug)
    {
        Title = title;
        Summary = summary;
        if (slug is not null)
        {
            Slug = slug;
        }
    }
}
