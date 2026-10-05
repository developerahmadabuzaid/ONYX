using Onyx.Application.Intelligence;
using Onyx.Application.Localization;
using Onyx.Application.Media;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Rule-based pre-publication checks over the working copy. Errors (invalid or misleading states) block approval and
/// publishing; warnings and recommendations inform the editor. No score is produced, and nothing here predicts rankings.
/// Extensible: future AI checks add findings, they never replace these rules.
/// </summary>
public sealed class ContentQualityGate(
    IContentRepository content,
    IContentPathRepository paths,
    IRedirectRepository redirects,
    IMediaRepository media,
    ICallToActionRepository ctas,
    IKeywordTargetRepository keywords,
    ContentPathService pathService)
{
    public const int MetaTitleGuidance = 60;
    public const int MetaDescriptionMinGuidance = 50;
    public const int MetaDescriptionMaxGuidance = 160;
    public const int ExcessiveLinksPerPage = 100;

    public async Task<QualityReport> EvaluateAsync(ContentItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var findings = new List<QualityFinding>();
        var snapshot = ContentSnapshot.From(item);

        if (item.Status == ContentStatus.Archived)
        {
            findings.Add(new(QualitySeverity.Error, "Lifecycle.Archived", "Archived content cannot be published. Restore it to a draft first."));
        }

        if (!item.HasPage)
        {
            findings.Add(new(QualitySeverity.Error, "Content.NoPage", "Taxonomy-only industries are tags without a page; there is nothing to publish."));
        }

        CheckCompleteness(item, findings);
        CheckProofAndProvenance(item, findings);
        await CheckSlugsAndPathsAsync(item, snapshot, findings, cancellationToken).ConfigureAwait(false);
        await CheckSeoAsync(item, findings, cancellationToken).ConfigureAwait(false);
        CheckHeadings(item, findings);
        await CheckReferencesAsync(item, snapshot, findings, cancellationToken).ConfigureAwait(false);

        return new QualityReport(findings);
    }

    /// <summary>Stage 7 guards, shown before publishing so editors are never surprised by the domain refusing it.</summary>
    private static void CheckProofAndProvenance(ContentItem item, List<QualityFinding> findings)
    {
        if (item.IsProofBearing && item.ProofVerification is null)
        {
            findings.Add(new(QualitySeverity.Error, "Proof.NotVerified",
                "This is public proof: someone with proof-verification rights must record who verified the current wording, against which source, and the client's permission."));
        }

        if (item.Type == ContentType.Insight && item.Origin == InsightOrigin.AiAssisted)
        {
            findings.Add(new(QualitySeverity.Recommendation, "Insight.AiAssisted",
                "AI-assisted: check every fact, figure and claim against approved company knowledge or a cited source before approving. Publishing needs a recorded human approval."));
        }

        if (item.Type == ContentType.Insight && item.AuthorId is null)
        {
            findings.Add(new(QualitySeverity.Recommendation, "Insight.OrganizationByline",
                "No author is credited, so the organization is shown as the author. Credit a real, consenting person only."));
        }
    }

    private static void CheckCompleteness(ContentItem item, List<QualityFinding> findings)
    {
        foreach (var language in SupportedLanguages.All)
        {
            var translation = item.GetTranslation(language);
            if (translation is null)
            {
                findings.Add(new(QualitySeverity.Error, "Translation.Missing", "This language has not been written yet; both languages are published together.", language));
                continue;
            }

            if (!translation.IsComplete)
            {
                findings.Add(new(QualitySeverity.Error, "Translation.Incomplete", "A title, a slug and content (summary or body) are required.", language));
            }

            if (translation.Body.Count == 0 && item.Type != ContentType.Industry)
            {
                findings.Add(new(QualitySeverity.Warning, "Content.EmptyBody", "The page has no body content beyond its summary.", language, "body"));
            }

            if (item.Type == ContentType.Industry && item.HasPage && !ContentItem.MeetsIndustryMinimum(translation))
            {
                findings.Add(new(QualitySeverity.Error, "Industry.ContentMinimum",
                    $"An industry page needs a summary and at least {ContentItem.IndustryMinimumSections} sections (H2 + content): context, business problems and Onyx's approach.",
                    language, "body"));
            }
        }
    }

    private async Task CheckSlugsAndPathsAsync(ContentItem item, ContentSnapshot snapshot, List<QualityFinding> findings, CancellationToken cancellationToken)
    {
        foreach (var translation in item.Translations)
        {
            if (translation.Slug.Length > Slug.RecommendedMaxLength)
            {
                findings.Add(new(QualitySeverity.Warning, "Slug.Long", $"Slugs longer than {Slug.RecommendedMaxLength} characters are harder to read and share.", translation.LanguageCode, "slug"));
            }

            if (await content.SlugTakenAsync(translation.LanguageCode, translation.SlugScope, translation.Slug, item.Id, cancellationToken).ConfigureAwait(false))
            {
                findings.Add(new(QualitySeverity.Error, "Slug.Conflict", "Another item in the same section uses this slug.", translation.LanguageCode, "slug"));
            }
        }

        if (!item.HasPage || snapshot.Translations.Count == 0)
        {
            return;
        }

        var computed = await pathService.ComputeAsync(item, snapshot, cancellationToken).ConfigureAwait(false);
        if (computed.IsFailure)
        {
            findings.Add(new(QualitySeverity.Error, computed.Error!.Code, computed.Error.Message));
            return;
        }

        foreach (var (language, path) in computed.Value)
        {
            var owner = await paths.FindAsync(language, path, cancellationToken).ConfigureAwait(false);
            if (owner is not null && owner.ContentItemId != item.Id)
            {
                findings.Add(new(QualitySeverity.Error, "Path.Taken", $"The URL /{language}/{path} belongs to other content (now or in its history).", language, "slug"));
            }
            else if (await redirects.FromPathExistsAsync(language, path, null, cancellationToken).ConfigureAwait(false))
            {
                findings.Add(new(QualitySeverity.Error, "Path.RedirectConflict", $"A redirect rule exists for /{language}/{path}; remove it first.", language, "slug"));
            }
        }
    }

    private async Task CheckSeoAsync(ContentItem item, List<QualityFinding> findings, CancellationToken cancellationToken)
    {
        foreach (var translation in item.Translations)
        {
            var language = translation.LanguageCode;
            var seo = translation.Seo;

            if (seo.MetaTitle is null)
            {
                findings.Add(new(QualitySeverity.Warning, "Seo.MetaTitleMissing", "No meta title: the page title will be used.", language, "metaTitle"));
            }
            else
            {
                if (seo.MetaTitle.Length > MetaTitleGuidance)
                {
                    findings.Add(new(QualitySeverity.Warning, "Seo.MetaTitleLong", $"Meta titles over ~{MetaTitleGuidance} characters are often truncated in results.", language, "metaTitle"));
                }

                if ((await content.FindByMetaTitleAsync(language, seo.MetaTitle, item.Id, cancellationToken).ConfigureAwait(false)).Count > 0)
                {
                    findings.Add(new(QualitySeverity.Warning, "Seo.DuplicateMetaTitle", "Another page uses the same meta title in this language.", language, "metaTitle"));
                }
            }

            if (seo.MetaDescription is null)
            {
                findings.Add(new(QualitySeverity.Warning, "Seo.MetaDescriptionMissing", "No meta description: search engines will choose a snippet themselves.", language, "metaDescription"));
            }
            else if (seo.MetaDescription.Length is < MetaDescriptionMinGuidance or > MetaDescriptionMaxGuidance)
            {
                findings.Add(new(QualitySeverity.Warning, "Seo.MetaDescriptionLength",
                    $"Meta descriptions of roughly {MetaDescriptionMinGuidance}-{MetaDescriptionMaxGuidance} characters display best.", language, "metaDescription"));
            }

            if (seo.PrimaryKeyword is null)
            {
                findings.Add(new(QualitySeverity.Recommendation, "Seo.NoPrimaryKeyword", "Choose the primary search topic this page should answer.", language, "primaryKeyword"));
            }
            else
            {
                await CheckCannibalizationAsync(item, translation, findings, cancellationToken).ConfigureAwait(false);
            }

            if (seo.SearchIntent == SearchIntent.Unspecified)
            {
                findings.Add(new(QualitySeverity.Recommendation, "Seo.NoSearchIntent", "Record the search intent the page serves.", language, "searchIntent"));
            }

            if (!seo.Index)
            {
                findings.Add(new(QualitySeverity.Warning, "Seo.NoIndex", "This language is set to noindex: it will be excluded from search engines and the sitemap.", language, "index"));
            }

            if (seo.CanonicalPath is not null)
            {
                findings.Add(new(QualitySeverity.Warning, "Seo.CanonicalOverride",
                    "A canonical override tells search engines another URL is the main version; this page will be left out of the sitemap. Use only when justified.", language, "canonicalPath"));
            }

            if (seo.StructuredData.HasFlag(StructuredDataTypes.FaqPage))
            {
                findings.Add(translation.Faqs.Count == 0
                    ? new(QualitySeverity.Error, "Schema.FaqWithoutVisibleFaqs", "FAQ structured data requires visible FAQs on the page.", language, "structuredData")
                    : new(QualitySeverity.Recommendation, "Schema.FaqNoRichResult",
                        "FAQ markup matches the visible FAQs and is valid, but Google no longer shows FAQ rich results (since May 2026). Keep FAQs only where they genuinely help visitors.", language, "structuredData"));
            }

            if (seo.StructuredData.HasFlag(StructuredDataTypes.Article) && item.Type != ContentType.Insight)
            {
                findings.Add(new(QualitySeverity.Error, "Schema.ArticleOnNonInsight", "Article structured data can only describe an insight.", language, "structuredData"));
            }

            if (seo.StructuredData.HasFlag(StructuredDataTypes.Service) && item.Type is not (ContentType.Service or ContentType.ServicePillar))
            {
                findings.Add(new(QualitySeverity.Error, "Schema.ServiceOnNonService", "Service structured data can only describe a service page.", language, "structuredData"));
            }

            if (seo.OpenGraphImageId is null && item.FeaturedImageId is null)
            {
                findings.Add(new(QualitySeverity.Recommendation, "Seo.NoSocialImage", "Add a featured or social image for link previews.", language, "openGraphImageId"));
            }
        }
    }

    private async Task CheckCannibalizationAsync(ContentItem item, ContentTranslation translation, List<QualityFinding> findings, CancellationToken cancellationToken)
    {
        var language = translation.LanguageCode;
        var normalized = translation.PrimaryKeywordNormalized!;
        var competing = await content.FindByPrimaryKeywordAsync(language, normalized, item.Id, cancellationToken).ConfigureAwait(false);
        if (competing.Count > 0)
        {
            findings.Add(new(QualitySeverity.Warning, "Seo.KeywordCannibalization",
                $"{competing.Count} other page(s) target the same primary keyword in this language; consider improving one page instead of competing pages.", language, "primaryKeyword"));
        }

        var mapped = await keywords.FindAsync(language, normalized, cancellationToken).ConfigureAwait(false);
        if (mapped.Any(target => target.TargetContentItemId is { } owner && owner != item.Id && target.Status != Domain.Intelligence.KeywordStatus.Retired))
        {
            findings.Add(new(QualitySeverity.Warning, "Seo.KeywordMapConflict", "The Keyword & Content Map assigns this keyword to different content.", language, "primaryKeyword"));
        }
    }

    private static void CheckHeadings(ContentItem item, List<QualityFinding> findings)
    {
        foreach (var translation in item.Translations)
        {
            var previous = 1;
            var headings = 0;
            var texts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var block in translation.Body.Where(block => block.Type == ContentBlockType.Heading))
            {
                headings++;
                var level = block.Level ?? 2;
                if (level > previous + 1)
                {
                    findings.Add(new(QualitySeverity.Warning, "Headings.SkippedLevel", $"Heading level jumps from H{previous} to H{level}.", translation.LanguageCode, "body"));
                }

                var text = string.Concat(block.Spans?.Select(span => span.Text) ?? []).Trim();
                if (!texts.Add(text))
                {
                    findings.Add(new(QualitySeverity.Recommendation, "Headings.Duplicate", $"The heading \"{text}\" appears more than once.", translation.LanguageCode, "body"));
                }

                previous = level;
            }

            if (headings == 0 && translation.Body.Count > 6)
            {
                findings.Add(new(QualitySeverity.Recommendation, "Headings.None", "Long content without H2 sections is hard to scan.", translation.LanguageCode, "body"));
            }
        }
    }

    private async Task CheckReferencesAsync(ContentItem item, ContentSnapshot snapshot, List<QualityFinding> findings, CancellationToken cancellationToken)
    {
        var linkedIds = item.Translations
            .SelectMany(translation => AllSpans(translation.Body))
            .Select(span => span.Link?.ContentItemId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var linked = await content.GetLookupAsync(linkedIds, cancellationToken).ConfigureAwait(false);

        var mediaReferences = Media.MediaUsageIndexer.References(snapshot).ToList();
        var assets = await media.GetLookupAsync(mediaReferences.Select(reference => reference.MediaAssetId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);

        var ctaIds = item.Translations.SelectMany(translation => translation.Body).Select(block => block.CtaId).OfType<Guid>()
            .Concat(item.PrimaryCtaId is { } primary ? [primary] : []).Distinct().ToList();
        var callsToAction = await ctas.GetLookupAsync(ctaIds, cancellationToken).ConfigureAwait(false);

        foreach (var translation in item.Translations)
        {
            var language = translation.LanguageCode;
            var spans = AllSpans(translation.Body).Where(span => span.Link is not null).ToList();
            var internalLinks = 0;

            foreach (var span in spans)
            {
                var link = span.Link!;
                if (link.ContentItemId is { } target)
                {
                    internalLinks++;
                    if (target == item.Id)
                    {
                        findings.Add(new(QualitySeverity.Recommendation, "Link.SelfLink", $"\"{span.Text}\" links to this page itself.", language, "body"));
                    }
                    else if (!linked.TryGetValue(target, out var targetItem))
                    {
                        findings.Add(new(QualitySeverity.Error, "Link.Broken", $"\"{span.Text}\" links to content that no longer exists.", language, "body"));
                    }
                    else if (!targetItem.IsLive)
                    {
                        findings.Add(new(QualitySeverity.Warning, "Link.TargetNotLive", $"\"{span.Text}\" links to content that is not live; it shows as plain text until that page is published.", language, "body"));
                    }
                }
                else if (link.Path is not null)
                {
                    internalLinks++;
                }
            }

            if (spans.Count > ExcessiveLinksPerPage)
            {
                findings.Add(new(QualitySeverity.Warning, "Links.Excessive", $"More than {ExcessiveLinksPerPage} links on one page dilutes their value and reads as spam.", language, "body"));
            }

            if (internalLinks == 0 && translation.Body.Count > 0)
            {
                findings.Add(new(QualitySeverity.Recommendation, "Links.NoInternalLinks", "Link to related Onyx pages where it genuinely helps the reader.", language, "body"));
            }

            foreach (var reference in mediaReferences.Where(reference => reference.LanguageCode == language))
            {
                if (!assets.TryGetValue(reference.MediaAssetId, out var asset))
                {
                    findings.Add(new(QualitySeverity.Error, "Media.Missing", $"A referenced image ({reference.Context}) no longer exists in the media library.", language));
                }
                else if (reference.Context == Domain.Media.MediaUsageContext.Body)
                {
                    var hasContextAlt = translation.Body.Any(block => block.MediaAssetId == asset.Id && !string.IsNullOrWhiteSpace(block.Alt));
                    if (!hasContextAlt && asset.DefaultAlt(language) is null)
                    {
                        findings.Add(new(QualitySeverity.Warning, "Media.AltMissing", $"An image ({asset.OriginalFileName}) has no alt text in this language.", language, "body"));
                    }
                }
            }

            foreach (var block in translation.Body.Where(block => block.CtaId is not null))
            {
                if (!callsToAction.TryGetValue(block.CtaId!.Value, out var cta) || !cta.IsActive)
                {
                    findings.Add(new(QualitySeverity.Error, "Cta.Missing", "A call-to-action block refers to a missing or inactive call to action.", language, "body"));
                }
            }
        }

        if (item.PrimaryCtaId is { } primaryCta && (!callsToAction.TryGetValue(primaryCta, out var primaryAction) || !primaryAction.IsActive))
        {
            findings.Add(new(QualitySeverity.Warning, "Cta.PrimaryMissing", "The selected call to action is missing or inactive; the default will be used."));
        }
    }

    private static IEnumerable<TextSpan> AllSpans(IReadOnlyList<ContentBlock> blocks) =>
        blocks.SelectMany(block => (block.Spans ?? []).Concat((block.Items ?? []).SelectMany(listItem => listItem.Spans)));
}
