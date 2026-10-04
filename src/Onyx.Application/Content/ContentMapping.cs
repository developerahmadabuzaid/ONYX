using Onyx.Application.Common;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Conversions between API DTOs and the content domain. Unknown enum names are validation errors, never defaults.</summary>
public static class ContentMapping
{
    private static readonly StructuredDataTypes[] StructuredDataValues =
        [StructuredDataTypes.WebPage, StructuredDataTypes.BreadcrumbList, StructuredDataTypes.Service, StructuredDataTypes.FaqPage];

    public static Result<IReadOnlyList<ContentBlock>> ToBlocks(IReadOnlyList<ContentBlockDto>? dtos)
    {
        if (dtos is null)
        {
            return Result.Success<IReadOnlyList<ContentBlock>>([]);
        }

        if (dtos.Count > ContentBodyRules.MaxBlocks)
        {
            return Invalid($"A body may contain at most {ContentBodyRules.MaxBlocks} blocks.");
        }

        var blocks = new List<ContentBlock>(dtos.Count);
        foreach (var dto in dtos)
        {
            if (dto is null || !EnumParsing.TryParse<ContentBlockType>(dto.Type, out var type))
            {
                return Invalid("Unknown block type.");
            }

            var spans = dto.Spans is null ? null : ToSpans(dto.Spans);
            if (spans is { IsFailure: true })
            {
                return spans.Error!;
            }

            List<ListItem>? items = null;
            if (dto.Items is not null)
            {
                items = [];
                foreach (var item in dto.Items)
                {
                    var itemSpans = ToSpans(item?.Spans ?? []);
                    if (itemSpans.IsFailure)
                    {
                        return itemSpans.Error!;
                    }

                    items.Add(new ListItem(itemSpans.Value));
                }
            }

            blocks.Add(new ContentBlock(type, dto.Level, spans?.Value, items, dto.Ordered, dto.MediaAssetId, Clean(dto.Alt), Clean(dto.Caption), dto.CtaId));
        }

        return blocks;
    }

    public static IReadOnlyList<FaqEntry> ToFaqs(IReadOnlyList<FaqDto>? dtos) =>
        (dtos ?? []).Select(faq => new FaqEntry(faq?.Question?.Trim() ?? string.Empty, faq?.Answer?.Trim() ?? string.Empty)).ToList();

    public static Result<SeoProfile> ToSeo(SeoProfileDto? dto)
    {
        if (dto is null)
        {
            return Invalid("SEO settings are required.");
        }

        var intent = SearchIntent.Unspecified;
        if (!string.IsNullOrWhiteSpace(dto.SearchIntent) && !EnumParsing.TryParse(dto.SearchIntent, out intent))
        {
            return Invalid("Unknown search intent.");
        }

        var status = SeoStatus.NotStarted;
        if (!string.IsNullOrWhiteSpace(dto.Status) && !EnumParsing.TryParse(dto.Status, out status))
        {
            return Invalid("Unknown SEO status.");
        }

        var structured = StructuredDataTypes.None;
        foreach (var name in dto.StructuredData ?? [])
        {
            if (!EnumParsing.TryParse<StructuredDataTypes>(name, out var flag) || !StructuredDataValues.Contains(flag))
            {
                return Invalid($"Unknown structured data type '{name}'.");
            }

            structured |= flag;
        }

        return new SeoProfile(
            dto.PrimaryKeyword, dto.SecondaryKeywords, intent, dto.MetaTitle, dto.MetaDescription, dto.CanonicalPath, dto.Index, dto.Follow,
            structured, dto.OpenGraphTitle, dto.OpenGraphDescription, dto.OpenGraphImageId, dto.TargetMarket?.Trim().ToUpperInvariant(), status, dto.Notes);
    }

    public static ContentBlockDto ToDto(ContentBlock block) => new(
        block.Type.ToString(),
        block.Level,
        block.Spans?.Select(ToDto).ToList(),
        block.Items?.Select(item => new ListItemDto(item.Spans.Select(ToDto).ToList())).ToList(),
        block.Ordered,
        block.MediaAssetId,
        block.Alt,
        block.Caption,
        block.CtaId);

    public static SeoProfileDto ToDto(SeoProfile seo) => new(
        seo.PrimaryKeyword,
        seo.SecondaryKeywords ?? [],
        seo.SearchIntent.ToString(),
        seo.MetaTitle,
        seo.MetaDescription,
        seo.CanonicalPath,
        seo.Index,
        seo.Follow,
        StructuredDataNames(seo.StructuredData),
        seo.OpenGraphTitle,
        seo.OpenGraphDescription,
        seo.OpenGraphImageId,
        seo.TargetMarket,
        seo.Status.ToString(),
        seo.Notes);

    public static IReadOnlyList<string> StructuredDataNames(StructuredDataTypes types) =>
        StructuredDataValues.Where(flag => types.HasFlag(flag)).Select(flag => flag.ToString()).ToList();

    public static ContentTranslationResponse ToResponse(ContentTranslation translation) => new(
        translation.LanguageCode, translation.Title, translation.Summary, translation.Slug,
        translation.Body.Select(ToDto).ToList(), translation.Faqs.Select(faq => new FaqDto(faq.Question, faq.Answer)).ToList(),
        ToDto(translation.Seo), translation.IsComplete);

    public static ContentTranslationResponse ToResponse(TranslationSnapshot translation) => new(
        translation.LanguageCode, translation.Title, translation.Summary, translation.Slug,
        translation.Body.Select(ToDto).ToList(), translation.Faqs.Select(faq => new FaqDto(faq.Question, faq.Answer)).ToList(),
        ToDto(translation.Seo),
        !string.IsNullOrWhiteSpace(translation.Title) && !string.IsNullOrWhiteSpace(translation.Slug)
            && (!string.IsNullOrWhiteSpace(translation.Summary) || translation.Body.Count > 0));

    public static ContentSnapshotResponse ToResponse(ContentSnapshot snapshot) => new(
        snapshot.Type.ToString(), snapshot.ParentId, snapshot.Key, snapshot.Presentation.ToString(), snapshot.SortOrder,
        snapshot.PrimaryCtaId, snapshot.FeaturedImageId, snapshot.Translations.Select(ToResponse).ToList());

    public static IReadOnlyDictionary<string, string> Titles(ContentItem item) =>
        item.Translations.ToDictionary(translation => translation.LanguageCode, translation => translation.Title, StringComparer.Ordinal);

    public static ContentSummaryResponse ToSummary(ContentItem item) => new(
        item.Id,
        item.Type.ToString(),
        item.ParentId,
        item.Key,
        item.Presentation.ToString(),
        item.Status.ToString(),
        item.IsLive,
        item.IsLive && item.Status != ContentStatus.Published,
        Titles(item),
        item.OfflineResolution.ToString(),
        item.LastPublishedAtUtc,
        item.ScheduledPublishAtUtc,
        item.ScheduledUnpublishAtUtc,
        item.UpdatedAtUtc ?? item.CreatedAtUtc);

    public static ContentDetailResponse ToDetail(ContentItem item, IReadOnlyList<ContentPath> paths) => new(
        item.Id,
        item.Type.ToString(),
        item.ParentId,
        item.Key,
        item.Presentation.ToString(),
        item.SortOrder,
        item.Status.ToString(),
        item.IsLive,
        item.IsLive && item.Status != ContentStatus.Published,
        item.CurrentRevisionNumber,
        item.PrimaryCtaId,
        item.FeaturedImageId,
        item.ReviewNote,
        item.SubmittedAtUtc,
        item.ApprovedAtUtc,
        item.ScheduledPublishAtUtc,
        item.ScheduledUnpublishAtUtc,
        item.ScheduleTimeZone,
        item.FirstPublishedAtUtc,
        item.LastPublishedAtUtc,
        item.OfflineResolution.ToString(),
        item.OfflineRedirectToId,
        item.Translations.OrderBy(translation => translation.LanguageCode, StringComparer.Ordinal).Select(ToResponse).ToList(),
        paths.OrderByDescending(path => path.IsCurrent).ThenByDescending(path => path.CreatedAtUtc)
            .Select(path => new ContentPathResponse(path.LanguageCode, path.Path, path.IsCurrent, path.CreatedAtUtc, path.RetiredAtUtc)).ToList(),
        item.Version);

    public static LocalizedTextDto ToDto(LocalizedText text) => new(text.LanguageCode, text.Text, text.Secondary);

    public static IReadOnlyList<LocalizedText> ToDomain(IReadOnlyList<LocalizedTextDto>? texts) =>
        (texts ?? []).Where(text => text is not null).Select(text => new LocalizedText(text.LanguageCode?.Trim() ?? string.Empty, text.Text ?? string.Empty, text.Secondary)).ToList();

    private static TextSpanDto ToDto(TextSpan span) =>
        new(span.Text, span.Emphasis == TextEmphasis.None ? null : span.Emphasis.ToString(),
            span.Link is null ? null : new LinkTargetDto(span.Link.ContentItemId, span.Link.Path, span.Link.Url));

    private static Result<IReadOnlyList<TextSpan>> ToSpans(IReadOnlyList<TextSpanDto> dtos)
    {
        if (dtos.Count > ContentBodyRules.MaxSpansPerBlock)
        {
            return Invalid($"Text blocks may contain at most {ContentBodyRules.MaxSpansPerBlock} text runs.");
        }

        var spans = new List<TextSpan>(dtos.Count);
        foreach (var dto in dtos)
        {
            var emphasis = TextEmphasis.None;
            if (dto is null || (!string.IsNullOrWhiteSpace(dto.Emphasis) && !EnumParsing.TryParse(dto.Emphasis, out emphasis)))
            {
                return Invalid("Unknown text emphasis.");
            }

            var link = dto.Link is null ? null : new LinkTarget(dto.Link.ContentItemId, Clean(dto.Link.Path), Clean(dto.Link.Url));
            spans.Add(new TextSpan(dto.Text ?? string.Empty, emphasis, link));
        }

        return spans;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Error Invalid(string message) => Error.Validation("Content.InvalidBody", message);
}
