using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Content;

/// <summary>
/// The working copy of one language of a <see cref="ContentItem"/>. Each language is authored independently (it is not a
/// translation of the other) but must describe the same underlying item. The public site never reads this row: it serves
/// the published revision snapshot.
/// </summary>
public sealed partial class ContentTranslation : AuditableEntity, IAuditable
{
    public const int LanguageCodeMaxLength = 10;
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 1_000;
    public const int SlugScopeMaxLength = 64;

    private ContentTranslation()
    {
        LanguageCode = string.Empty;
        Title = string.Empty;
        Summary = string.Empty;
        Slug = string.Empty;
        SlugScope = string.Empty;
        Body = [];
        Faqs = [];
        SecondaryKeywords = [];
    }

    public Guid ContentItemId { get; private set; }

    public string LanguageCode { get; private set; }

    public string Title { get; private set; }

    public string Summary { get; private set; }

    public string Slug { get; private set; }

    /// <summary>Sibling namespace (type + parent) in which the slug must be unique for this language.</summary>
    public string SlugScope { get; private set; }

    /// <summary>Large; preserved in revisions, so excluded from per-field audit change sets.</summary>
    [NotAudited]
    public IReadOnlyList<ContentBlock> Body { get; private set; }

    [NotAudited]
    public IReadOnlyList<FaqEntry> Faqs { get; private set; }

    public string? PrimaryKeyword { get; private set; }

    /// <summary>Comparison form of <see cref="PrimaryKeyword"/> for cannibalization checks.</summary>
    [NotAudited]
    public string? PrimaryKeywordNormalized { get; private set; }

    public IReadOnlyList<string> SecondaryKeywords { get; private set; }

    public SearchIntent SearchIntent { get; private set; }

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public string? CanonicalPath { get; private set; }

    public bool RobotsIndex { get; private set; } = true;

    public bool RobotsFollow { get; private set; } = true;

    public StructuredDataTypes StructuredData { get; private set; } = StructuredDataTypes.WebPage | StructuredDataTypes.BreadcrumbList;

    public string? OpenGraphTitle { get; private set; }

    public string? OpenGraphDescription { get; private set; }

    public Guid? OpenGraphImageId { get; private set; }

    public string? TargetMarket { get; private set; }

    public SeoStatus SeoStatus { get; private set; }

    public string? SeoNotes { get; private set; }

    /// <summary>Title, slug and some content (summary or body) exist. Required in every language before approval.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Slug) && (!string.IsNullOrWhiteSpace(Summary) || Body.Count > 0);

    public SeoProfile Seo => new(
        PrimaryKeyword, SecondaryKeywords, SearchIntent, MetaTitle, MetaDescription, CanonicalPath, RobotsIndex, RobotsFollow,
        StructuredData, OpenGraphTitle, OpenGraphDescription, OpenGraphImageId, TargetMarket, SeoStatus, SeoNotes);

    internal static ContentTranslation Create(Guid contentItemId, string languageCode, string slugScope) =>
        new() { ContentItemId = contentItemId, LanguageCode = languageCode, SlugScope = slugScope };

    internal void SetSlugScope(string slugScope) => SlugScope = slugScope;

    internal Result Update(string title, string? summary, string slug, IReadOnlyList<ContentBlock> body, IReadOnlyList<FaqEntry> faqs)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(faqs);
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var trimmedSummary = summary?.Trim() ?? string.Empty;

        if (trimmedTitle.Length is 0 or > TitleMaxLength || ContentBodyRules.HasControlCharacters(trimmedTitle, allowNewLines: false))
        {
            return Error.Validation("Content.InvalidTitle", $"A title of 1-{TitleMaxLength} characters is required.");
        }

        if (trimmedSummary.Length > SummaryMaxLength || ContentBodyRules.HasControlCharacters(trimmedSummary, allowNewLines: true))
        {
            return Error.Validation("Content.InvalidSummary", $"The summary is limited to {SummaryMaxLength} characters of plain text.");
        }

        if (!Content.Slug.IsValid(slug))
        {
            return Error.Validation("Content.InvalidSlug", "The slug must be normalized: Arabic letters, a-z, 0-9 and single hyphens.");
        }

        if (ContentBodyRules.Validate(body) is { } bodyError)
        {
            return bodyError;
        }

        if (ContentBodyRules.ValidateFaqs(faqs) is { } faqError)
        {
            return faqError;
        }

        Title = trimmedTitle;
        Summary = trimmedSummary;
        Slug = slug;
        Body = [.. body];
        Faqs = [.. faqs];
        return Result.Success();
    }

    internal Result UpdateSeo(SeoProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (Validate(profile) is { } error)
        {
            return error;
        }

        PrimaryKeyword = Clean(profile.PrimaryKeyword);
        PrimaryKeywordNormalized = PrimaryKeyword is null ? null : TextNormalization.NormalizeKeyword(PrimaryKeyword);
        SecondaryKeywords = (profile.SecondaryKeywords ?? [])
            .Select(keyword => keyword.Trim())
            .Where(keyword => keyword.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        SearchIntent = profile.SearchIntent;
        MetaTitle = Clean(profile.MetaTitle);
        MetaDescription = Clean(profile.MetaDescription);
        CanonicalPath = Clean(profile.CanonicalPath);
        RobotsIndex = profile.Index;
        RobotsFollow = profile.Follow;
        StructuredData = profile.StructuredData;
        OpenGraphTitle = Clean(profile.OpenGraphTitle);
        OpenGraphDescription = Clean(profile.OpenGraphDescription);
        OpenGraphImageId = profile.OpenGraphImageId;
        TargetMarket = Clean(profile.TargetMarket);
        SeoStatus = profile.Status;
        SeoNotes = Clean(profile.Notes);
        return Result.Success();
    }

    public static Error? Validate(SeoProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        static bool TooLong(string? value, int max) => value is not null && (value.Trim().Length > max || ContentBodyRules.HasControlCharacters(value, false));

        if (TooLong(profile.PrimaryKeyword, SeoProfile.KeywordMaxLength)
            || profile.SecondaryKeywords is { } secondary && (secondary.Count > SeoProfile.MaxSecondaryKeywords || secondary.Any(keyword => keyword is null || TooLong(keyword, SeoProfile.KeywordMaxLength))))
        {
            return SeoInvalid($"Keywords are limited to {SeoProfile.KeywordMaxLength} characters; at most {SeoProfile.MaxSecondaryKeywords} secondary keywords.");
        }

        if (TooLong(profile.MetaTitle, SeoProfile.MetaTitleMaxLength) || TooLong(profile.MetaDescription, SeoProfile.MetaDescriptionMaxLength)
            || TooLong(profile.OpenGraphTitle, SeoProfile.OpenGraphTitleMaxLength) || TooLong(profile.OpenGraphDescription, SeoProfile.OpenGraphDescriptionMaxLength))
        {
            return SeoInvalid("Meta and social titles/descriptions exceed their maximum length.");
        }

        if (profile.Notes is { Length: > SeoProfile.NotesMaxLength })
        {
            return SeoInvalid($"SEO notes are limited to {SeoProfile.NotesMaxLength} characters.");
        }

        if (!string.IsNullOrWhiteSpace(profile.CanonicalPath) && !ContentBodyRules.IsSafeRelativePath(profile.CanonicalPath.Trim()))
        {
            return SeoInvalid("A canonical override must be a site path starting with a single '/'.");
        }

        if (!string.IsNullOrWhiteSpace(profile.TargetMarket) && !MarketCode().IsMatch(profile.TargetMarket.Trim()))
        {
            return SeoInvalid("The target market must be an ISO 3166-1 alpha-2 country code, e.g. SA.");
        }

        const StructuredDataTypes known = StructuredDataTypes.WebPage | StructuredDataTypes.BreadcrumbList | StructuredDataTypes.Service | StructuredDataTypes.FaqPage;
        return (profile.StructuredData & ~known) != 0 || !Enum.IsDefined(profile.SearchIntent) || !Enum.IsDefined(profile.Status)
            ? SeoInvalid("Unknown structured data, search intent or SEO status value.")
            : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error SeoInvalid(string message) => Error.Validation("Content.InvalidSeo", message);

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MarketCode();
}
