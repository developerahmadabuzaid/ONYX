using Onyx.Application.Localization;
using Onyx.Application.Media;
using Onyx.Contracts.Content;
using Onyx.Domain.Content;
using Onyx.Domain.Media;

namespace Onyx.Application.Content;

/// <summary>
/// Builds the public representation of one language of a content snapshot: links resolved to current URLs (links to offline
/// content become plain text), images with contextual alt text, breadcrumbs, related live content, the resolved call to action,
/// and only the structured data the visible content supports.
/// </summary>
public sealed class PublicContentBuilder(
    IContentRepository content,
    IContentPathRepository paths,
    IContentRevisionRepository revisions,
    IContentRelationRepository relations,
    IContentClusterRepository clusters,
    IMediaRepository media,
    ICallToActionRepository ctas,
    CtaResolver ctaResolver,
    Insights.IAuthorRepository authors,
    Proof.IProofItemRepository proofItems)
{
    private const int MaxAncestors = 10;
    private const int MaxRelated = 6;

    public async Task<PublicContentResponse> BuildAsync(
        ContentItem item, ContentSnapshot snapshot, string languageCode, string? selfPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(snapshot);
        var translation = snapshot.Translation(languageCode) ?? throw new InvalidOperationException("The snapshot has no translation in this language.");

        var ancestors = await LoadAncestorsAsync(snapshot.ParentId, cancellationToken).ConfigureAwait(false);
        var relatedIds = await RelatedIdsAsync(item.Id, cancellationToken).ConfigureAwait(false);
        var linkIds = AllSpans(translation.Body).Select(span => span.Link?.ContentItemId).OfType<Guid>().Distinct().ToList();

        var lookup = await content.GetLookupAsync(linkIds.Concat(relatedIds).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        var liveIds = lookup.Values.Where(candidate => candidate.IsLive).Select(candidate => candidate.Id)
            .Concat(ancestors.Select(ancestor => ancestor.Item.Id)).Append(item.Id).Distinct().ToList();
        var currentPaths = await paths.GetCurrentPathsAsync(liveIds, cancellationToken).ConfigureAwait(false);

        string? PathOf(Guid id, string language) =>
            currentPaths.TryGetValue((id, language), out var path) ? $"/{language}/{path}" : null;

        var mediaIds = MediaUsageIndexer.References(new ContentSnapshot(snapshot.Type, null, null, snapshot.Presentation, 0, null, snapshot.FeaturedImageId, [translation]))
            .Select(reference => reference.MediaAssetId).Distinct().ToList();
        var assets = await media.GetLookupAsync(mediaIds, cancellationToken).ConfigureAwait(false);

        var blockCtaIds = translation.Body.Select(block => block.CtaId).OfType<Guid>().Distinct().ToList();
        var blockCtas = await ctas.GetLookupAsync(blockCtaIds, cancellationToken).ConfigureAwait(false);

        var body = new List<PublicBlockResponse>(translation.Body.Count);
        foreach (var block in translation.Body)
        {
            var mapped = MapBlock(block, languageCode, lookup, PathOf, assets, blockCtas);
            if (mapped is not null)
            {
                body.Add(mapped);
            }
        }

        var breadcrumbs = ancestors
            .Select(ancestor => (Title: ancestor.Translation(languageCode)?.Title, Path: PathOf(ancestor.Item.Id, languageCode)))
            .Where(crumb => crumb.Title is not null && crumb.Path is not null)
            .Select(crumb => new BreadcrumbResponse(crumb.Title!, crumb.Path!))
            .ToList();
        var ownPath = selfPath ?? PathOf(item.Id, languageCode);
        if (ownPath is not null)
        {
            breadcrumbs.Add(new BreadcrumbResponse(translation.Title, ownPath));
        }

        var related = await BuildRelatedAsync(relatedIds, lookup, languageCode, PathOf, cancellationToken).ConfigureAwait(false);
        var pillarCta = ancestors.FirstOrDefault(ancestor => ancestor.Snapshot.Type == ContentType.ServicePillar)?.Snapshot.PrimaryCtaId;
        var cta = await ctaResolver.ResolveAsync(snapshot.PrimaryCtaId, pillarCta, snapshot.Type, languageCode, cancellationToken).ConfigureAwait(false);

        // hreflang links only canonical, indexable pages: a noindex or canonicalized page declares no alternates and is never
        // listed as one (the same rule the sitemap uses, so HTML and sitemap annotations always agree).
        var alternates = item.IsLive && IsHreflangEligible(snapshot, languageCode)
            ? SupportedLanguages.All
                .Select(language => (language, path: PathOf(item.Id, language)))
                .Where(entry => entry.path is not null && IsHreflangEligible(snapshot, entry.language))
                .ToDictionary(entry => entry.language, entry => entry.path!, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        var seo = translation.Seo;
        var structured = SupportedStructuredData(snapshot.Type, seo.StructuredData, translation.Faqs.Count, breadcrumbs.Count);
        var canonical = seo.CanonicalPath ?? ownPath ?? $"/{languageCode}/";

        return new PublicContentResponse(
            item.Id,
            snapshot.Type.ToString(),
            languageCode,
            translation.Title,
            translation.Summary,
            body,
            translation.Faqs.Select(faq => new FaqDto(faq.Question, faq.Answer)).ToList(),
            new PublicSeoResponse(
                seo.MetaTitle ?? translation.Title,
                seo.MetaDescription ?? (string.IsNullOrWhiteSpace(translation.Summary) ? null : translation.Summary),
                canonical,
                seo.Index,
                seo.Follow,
                alternates,
                alternates.GetValueOrDefault(SupportedLanguages.Default),
                seo.OpenGraphTitle,
                seo.OpenGraphDescription,
                seo.OpenGraphImageId is { } openGraph && assets.TryGetValue(openGraph, out var openGraphAsset) ? ToMedia(openGraphAsset, null, null, languageCode) : null,
                structured),
            breadcrumbs,
            related,
            cta is null ? null : ToCta(cta, languageCode),
            snapshot.FeaturedImageId is { } featured && assets.TryGetValue(featured, out var featuredAsset) ? ToMedia(featuredAsset, null, null, languageCode) : null,
            item.FirstPublishedAtUtc,
            item.GetTranslation(languageCode)?.LiveContentModifiedAtUtc ?? item.LastPublishedAtUtc,
            snapshot.Type == ContentType.Insight ? await InsightAsync(snapshot, languageCode, cancellationToken).ConfigureAwait(false) : null,
            snapshot.Type is ContentType.Project or ContentType.CaseStudy ? await ProofAsync(item.Id, languageCode, cancellationToken).ConfigureAwait(false) : null);
    }

    /// <summary>Indexable and self-canonical in <paramref name="languageCode"/>: the only pages hreflang and the sitemap may list.</summary>
    public static bool IsHreflangEligible(ContentSnapshot snapshot, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Translation(languageCode) is { Seo: { Index: true, CanonicalPath: null } };
    }

    public static IReadOnlyList<string> SupportedStructuredData(ContentType type, StructuredDataTypes selected, int faqCount, int breadcrumbCount)
    {
        var types = new List<string>();
        if (selected.HasFlag(StructuredDataTypes.WebPage))
        {
            types.Add(nameof(StructuredDataTypes.WebPage));
        }

        if (selected.HasFlag(StructuredDataTypes.BreadcrumbList) && breadcrumbCount > 1)
        {
            types.Add(nameof(StructuredDataTypes.BreadcrumbList));
        }

        if (selected.HasFlag(StructuredDataTypes.Service) && type is ContentType.Service or ContentType.ServicePillar)
        {
            types.Add(nameof(StructuredDataTypes.Service));
        }

        if (selected.HasFlag(StructuredDataTypes.FaqPage) && faqCount > 0)
        {
            types.Add(nameof(StructuredDataTypes.FaqPage));
        }

        if (selected.HasFlag(StructuredDataTypes.Article) && type == ContentType.Insight)
        {
            types.Add(nameof(StructuredDataTypes.Article));
        }

        return types;
    }

    /// <summary>Byline from the snapshot: a credited author only while that author exists and is active, otherwise the organization.</summary>
    private async Task<PublicInsightResponse> InsightAsync(ContentSnapshot snapshot, string languageCode, CancellationToken cancellationToken)
    {
        PublicAuthorResponse? author = null;
        if (snapshot.AuthorId is { } authorId && await authors.GetAsync(authorId, cancellationToken).ConfigureAwait(false) is { IsActive: true } credited)
        {
            var name = credited.Names.FirstOrDefault(text => text.LanguageCode == languageCode) ?? credited.Names[0];
            author = new PublicAuthorResponse(name.Text, name.Secondary);
        }

        return new PublicInsightResponse((snapshot.InsightKind ?? InsightKind.Article).ToString(), snapshot.Origin.ToString(), author);
    }

    /// <summary>Verified, published proof written in this language.</summary>
    private async Task<PublicProofResponse> ProofAsync(Guid contentItemId, string languageCode, CancellationToken cancellationToken)
    {
        var published = await proofItems.ListPublishedForContentAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        List<PublicProofItemResponse> Of(Domain.Proof.ProofItemKind kind) => published
            .Where(item => item.Kind == kind && item.Verification is not null)
            .Select(item => (item, text: item.Texts.FirstOrDefault(text => text.LanguageCode == languageCode)))
            .Where(entry => entry.text is not null)
            .Select(entry => new PublicProofItemResponse(
                entry.text!.Text, entry.text.Secondary, entry.item.Value, entry.item.AsOf, entry.item.AttributionName, entry.item.AttributionRole, entry.item.AttributionOrganization))
            .ToList();

        return new PublicProofResponse(Of(Domain.Proof.ProofItemKind.Capability), Of(Domain.Proof.ProofItemKind.Metric), Of(Domain.Proof.ProofItemKind.Testimonial));
    }

    public static PublicMediaResponse ToMedia(MediaAsset asset, string? altOverride, string? captionOverride, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var defaults = asset.Texts.FirstOrDefault(text => text.LanguageCode == languageCode);
        return new PublicMediaResponse(
            asset.Id, asset.OriginalFileName, asset.ContentType, asset.Width, asset.Height,
            altOverride ?? asset.DefaultAlt(languageCode), captionOverride ?? defaults?.Secondary);
    }

    private static PublicCtaResponse ToCta(CallToAction cta, string languageCode)
    {
        var text = cta.Texts.First(entry => entry.LanguageCode == languageCode);
        return new PublicCtaResponse(cta.ActionType.ToString(), text.Text, text.Secondary, cta.ActionType == CtaActionType.Url ? cta.TargetUrl : null);
    }

    private static PublicBlockResponse? MapBlock(
        ContentBlock block,
        string languageCode,
        IReadOnlyDictionary<Guid, ContentItem> lookup,
        Func<Guid, string, string?> pathOf,
        IReadOnlyDictionary<Guid, MediaAsset> assets,
        IReadOnlyDictionary<Guid, CallToAction> blockCtas)
    {
        IReadOnlyList<PublicSpanResponse> Spans(IReadOnlyList<TextSpan> spans) => spans.Select(span =>
        {
            string? href = null;
            var external = false;
            if (span.Link is { } link)
            {
                if (link.ContentItemId is { } target)
                {
                    href = lookup.TryGetValue(target, out var targetItem) && targetItem.IsLive ? pathOf(target, languageCode) : null;
                }
                else if (link.Path is not null)
                {
                    href = link.Path;
                }
                else
                {
                    href = link.Url;
                    external = true;
                }
            }

            return new PublicSpanResponse(span.Text, span.Emphasis == TextEmphasis.None ? null : span.Emphasis.ToString(), href, external);
        }).ToList();

        return block.Type switch
        {
            ContentBlockType.Heading or ContentBlockType.Paragraph =>
                new PublicBlockResponse(block.Type.ToString(), block.Level, Spans(block.Spans ?? []), null, null, null, null),
            ContentBlockType.List =>
                new PublicBlockResponse(block.Type.ToString(), null, null, (block.Items ?? []).Select(item => Spans(item.Spans)).ToList(), block.Ordered ?? false, null, null),
            ContentBlockType.Image when block.MediaAssetId is { } image && assets.TryGetValue(image, out var asset) =>
                new PublicBlockResponse(block.Type.ToString(), null, null, null, null, ToMedia(asset, block.Alt, block.Caption, languageCode), null),
            ContentBlockType.CallToAction when block.CtaId is { } ctaId && blockCtas.TryGetValue(ctaId, out var cta)
                && cta.IsActive && cta.Texts.Any(text => text.LanguageCode == languageCode) =>
                new PublicBlockResponse(block.Type.ToString(), null, null, null, null, null, ToCta(cta, languageCode)),
            _ => null,
        };
    }

    private async Task<List<AncestorInfo>> LoadAncestorsAsync(Guid? parentId, CancellationToken cancellationToken)
    {
        var chain = new List<AncestorInfo>();
        var next = parentId;
        while (next is { } currentId && chain.Count < MaxAncestors)
        {
            var parents = await content.GetLookupAsync([currentId], cancellationToken).ConfigureAwait(false);
            if (!parents.TryGetValue(currentId, out var parent) || !parent.IsLive)
            {
                break;
            }

            var revision = await revisions.GetAsync(parent.LiveRevisionId!.Value, cancellationToken).ConfigureAwait(false);
            if (revision is null)
            {
                break;
            }

            chain.Insert(0, new AncestorInfo(parent, ContentSnapshotSerializer.Deserialize(revision.Snapshot)));
            next = parent.ParentId;
        }

        return chain;
    }

    private async Task<List<Guid>> RelatedIdsAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var manual = (await relations.ListFromAsync(itemId, cancellationToken).ConfigureAwait(false))
            .OrderBy(relation => relation.Position).Select(relation => relation.ToContentItemId).ToList();
        if (manual.Count > 0)
        {
            return manual;
        }

        // Fallback: other members of the same topic clusters (an editorial grouping, not a performance signal).
        return (await clusters.ListContainingAsync(itemId, cancellationToken).ConfigureAwait(false))
            .Where(cluster => cluster.IsActive)
            .SelectMany(cluster => cluster.Members)
            .Select(member => member.ContentItemId)
            .Where(id => id != itemId)
            .Distinct()
            .ToList();
    }

    private async Task<List<RelatedContentResponse>> BuildRelatedAsync(
        List<Guid> relatedIds, IReadOnlyDictionary<Guid, ContentItem> lookup, string languageCode, Func<Guid, string, string?> pathOf, CancellationToken cancellationToken)
    {
        var live = relatedIds.Where(id => lookup.TryGetValue(id, out var candidate) && candidate.IsLive && candidate.HasPage).Take(MaxRelated).ToList();
        var liveRevisions = await revisions.GetManyAsync(live.Select(id => lookup[id].LiveRevisionId!.Value).ToList(), cancellationToken).ConfigureAwait(false);
        var result = new List<RelatedContentResponse>();
        foreach (var id in live)
        {
            var related = lookup[id];
            if (!liveRevisions.TryGetValue(related.LiveRevisionId!.Value, out var revision) || pathOf(id, languageCode) is not { } path)
            {
                continue;
            }

            var relatedTranslation = ContentSnapshotSerializer.Deserialize(revision.Snapshot).Translation(languageCode);
            if (relatedTranslation is not null)
            {
                result.Add(new RelatedContentResponse(related.Type.ToString(), relatedTranslation.Title,
                    string.IsNullOrWhiteSpace(relatedTranslation.Summary) ? null : relatedTranslation.Summary, path));
            }
        }

        return result;
    }

    private static IEnumerable<TextSpan> AllSpans(IReadOnlyList<ContentBlock> blocks) =>
        blocks.SelectMany(block => (block.Spans ?? []).Concat((block.Items ?? []).SelectMany(listItem => listItem.Spans)));

    private sealed record AncestorInfo(ContentItem Item, ContentSnapshot Snapshot)
    {
        public TranslationSnapshot? Translation(string languageCode) => Snapshot.Translation(languageCode);
    }
}
