using System.Globalization;
using System.Text;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Media;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public sealed class ContentRevisionService(
    IContentRepository content,
    IContentRevisionRepository revisions,
    IContentPathRepository paths,
    ContentAccess access,
    ContentRevisionRecorder revisionRecorder,
    MediaUsageIndexer mediaUsage,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork) : IContentRevisionService
{
    public const int MaxListed = 200;
    private const int MaxDiffValueLength = 4_000;

    private static readonly Error RevisionNotFound = Error.NotFound("Revision.NotFound", "Revision not found.");

    public async Task<Result<IReadOnlyList<ContentRevisionSummaryResponse>>> ListAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var list = await revisions.ListAsync(contentItemId, MaxListed, cancellationToken).ConfigureAwait(false);
        return list.Select(revision => ToSummary(revision, loaded.Value.Item)).ToList();
    }

    public async Task<Result<ContentRevisionResponse>> GetAsync(Guid contentItemId, int number, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var revision = await revisions.GetByNumberAsync(contentItemId, number, cancellationToken).ConfigureAwait(false);
        return revision is null
            ? RevisionNotFound
            : new ContentRevisionResponse(ToSummary(revision, loaded.Value.Item), ContentMapping.ToResponse(ContentSnapshotSerializer.Deserialize(revision.Snapshot)));
    }

    public async Task<Result<RevisionComparisonResponse>> CompareAsync(Guid contentItemId, int fromNumber, int toNumber, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var from = await revisions.GetByNumberAsync(contentItemId, fromNumber, cancellationToken).ConfigureAwait(false);
        var to = await revisions.GetByNumberAsync(contentItemId, toNumber, cancellationToken).ConfigureAwait(false);
        if (from is null || to is null)
        {
            return RevisionNotFound;
        }

        return new RevisionComparisonResponse(fromNumber, toNumber,
            Compare(ContentSnapshotSerializer.Deserialize(from.Snapshot), ContentSnapshotSerializer.Deserialize(to.Snapshot)));
    }

    public async Task<Result<ContentDetailResponse>> RestoreAsync(Guid contentItemId, int number, RestoreRevisionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await access.ForEditAsync(contentItemId, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        var source = await revisions.GetByNumberAsync(contentItemId, number, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return RevisionNotFound;
        }

        var snapshot = ContentSnapshotSerializer.Deserialize(source.Snapshot);
        var prepared = item.PrepareRestore();
        if (prepared.IsFailure)
        {
            return prepared.Error!;
        }

        if (snapshot.ParentId is { } parentId && !(await content.GetLookupAsync([parentId], cancellationToken).ConfigureAwait(false)).ContainsKey(parentId))
        {
            return Error.Conflict("Revision.ParentMissing", "The parent page of that revision no longer exists.");
        }

        var settings = item.UpdateSettings(snapshot.ParentId, snapshot.Key, snapshot.Presentation, snapshot.SortOrder, snapshot.PrimaryCtaId, snapshot.FeaturedImageId);
        if (settings.IsFailure)
        {
            return settings.Error!;
        }

        foreach (var translation in snapshot.Translations)
        {
            if (await content.SlugTakenAsync(translation.LanguageCode, ContentItem.BuildSlugScope(item.Type, item.ParentId), translation.Slug, item.Id, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Error.Conflict("Revision.SlugTaken", $"The {translation.LanguageCode} slug of that revision is now used by other content.");
            }

            var updated = item.UpdateTranslation(translation.LanguageCode, translation.Title, translation.Summary, translation.Slug, translation.Body, translation.Faqs);
            if (updated.IsFailure)
            {
                return updated.Error!;
            }

            var seo = item.UpdateSeo(translation.LanguageCode, translation.Seo);
            if (seo.IsFailure)
            {
                return seo.Error!;
            }
        }

        foreach (var language in item.Translations.Select(translation => translation.LanguageCode).Except(snapshot.Translations.Select(translation => translation.LanguageCode)).ToList())
        {
            item.RemoveTranslation(language);
        }

        var restored = await revisionRecorder.RecordAsync(item, RevisionKind.Restored, request.Note ?? $"Restored from revision {number}", source.Id, cancellationToken)
            .ConfigureAwait(false);
        var live = item.LiveRevisionId is { } liveId && await revisions.GetAsync(liveId, cancellationToken).ConfigureAwait(false) is { } liveRevision
            ? ContentSnapshotSerializer.Deserialize(liveRevision.Snapshot)
            : null;
        await mediaUsage.RebuildAsync(item.Id, ContentSnapshot.From(item), live, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.ContentRevisionRestored, AuditCategory.Content, nameof(ContentItem), item.Id.ToString(),
            new { fromRevision = number, newRevision = restored.Number });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ContentMapping.ToDetail(item, await paths.ListForItemAsync(item.Id, cancellationToken).ConfigureAwait(false));
    }

    public static IReadOnlyList<FieldDifferenceResponse> Compare(ContentSnapshot before, ContentSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var differences = new List<FieldDifferenceResponse>();

        void Add(string? language, string field, string? oldValue, string? newValue)
        {
            if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                differences.Add(new FieldDifferenceResponse(language, field, Truncate(oldValue), Truncate(newValue)));
            }
        }

        Add(null, "parentId", before.ParentId?.ToString(), after.ParentId?.ToString());
        Add(null, "key", before.Key, after.Key);
        Add(null, "presentation", before.Presentation.ToString(), after.Presentation.ToString());
        Add(null, "sortOrder", before.SortOrder.ToString(CultureInfo.InvariantCulture), after.SortOrder.ToString(CultureInfo.InvariantCulture));
        Add(null, "primaryCtaId", before.PrimaryCtaId?.ToString(), after.PrimaryCtaId?.ToString());
        Add(null, "featuredImageId", before.FeaturedImageId?.ToString(), after.FeaturedImageId?.ToString());

        var languages = before.Translations.Select(translation => translation.LanguageCode)
            .Union(after.Translations.Select(translation => translation.LanguageCode)).OrderBy(language => language, StringComparer.Ordinal);
        foreach (var language in languages)
        {
            var oldTranslation = before.Translation(language);
            var newTranslation = after.Translation(language);
            Add(language, "title", oldTranslation?.Title, newTranslation?.Title);
            Add(language, "summary", oldTranslation?.Summary, newTranslation?.Summary);
            Add(language, "slug", oldTranslation?.Slug, newTranslation?.Slug);
            Add(language, "body", BodyText(oldTranslation?.Body), BodyText(newTranslation?.Body));
            Add(language, "faqs", FaqText(oldTranslation?.Faqs), FaqText(newTranslation?.Faqs));

            var oldSeo = oldTranslation?.Seo;
            var newSeo = newTranslation?.Seo;
            Add(language, "primaryKeyword", oldSeo?.PrimaryKeyword, newSeo?.PrimaryKeyword);
            Add(language, "secondaryKeywords", Join(oldSeo?.SecondaryKeywords), Join(newSeo?.SecondaryKeywords));
            Add(language, "searchIntent", oldSeo?.SearchIntent.ToString(), newSeo?.SearchIntent.ToString());
            Add(language, "metaTitle", oldSeo?.MetaTitle, newSeo?.MetaTitle);
            Add(language, "metaDescription", oldSeo?.MetaDescription, newSeo?.MetaDescription);
            Add(language, "canonicalPath", oldSeo?.CanonicalPath, newSeo?.CanonicalPath);
            Add(language, "robots", Robots(oldSeo), Robots(newSeo));
            Add(language, "structuredData", oldSeo?.StructuredData.ToString(), newSeo?.StructuredData.ToString());
            Add(language, "openGraphTitle", oldSeo?.OpenGraphTitle, newSeo?.OpenGraphTitle);
            Add(language, "openGraphDescription", oldSeo?.OpenGraphDescription, newSeo?.OpenGraphDescription);
            Add(language, "openGraphImageId", oldSeo?.OpenGraphImageId?.ToString(), newSeo?.OpenGraphImageId?.ToString());
            Add(language, "targetMarket", oldSeo?.TargetMarket, newSeo?.TargetMarket);
            Add(language, "seoStatus", oldSeo?.Status.ToString(), newSeo?.Status.ToString());
            Add(language, "seoNotes", oldSeo?.Notes, newSeo?.Notes);
        }

        return differences;
    }

    /// <summary>Readable plain-text form of a body for comparison.</summary>
    public static string? BodyText(IReadOnlyList<ContentBlock>? body)
    {
        if (body is null)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var block in body)
        {
            var line = block.Type switch
            {
                ContentBlockType.Heading => new string('#', block.Level ?? 2) + " " + Spans(block.Spans),
                ContentBlockType.Paragraph => Spans(block.Spans),
                ContentBlockType.List => string.Join('\n', (block.Items ?? []).Select(item => "- " + Spans(item.Spans))),
                ContentBlockType.Image => $"[image {block.MediaAssetId}] {block.Alt}",
                ContentBlockType.CallToAction => $"[call to action {block.CtaId}]",
                _ => string.Empty,
            };
            text.AppendLine(line);
        }

        return text.ToString();
    }

    private static string Spans(IReadOnlyList<TextSpan>? spans) =>
        string.Concat((spans ?? []).Select(span => span.Link is null ? span.Text : $"[{span.Text}]({span.Link.ContentItemId?.ToString() ?? span.Link.Path ?? span.Link.Url})"));

    private static string? FaqText(IReadOnlyList<FaqEntry>? faqs) =>
        faqs is null ? null : string.Join("\n", faqs.Select(faq => $"Q: {faq.Question}\nA: {faq.Answer}"));

    private static string? Join(IReadOnlyList<string>? values) => values is null ? null : string.Join(", ", values);

    private static string? Robots(SeoProfile? seo) => seo is null ? null : $"{(seo.Index ? "index" : "noindex")}, {(seo.Follow ? "follow" : "nofollow")}";

    private static string? Truncate(string? value) => value is { Length: > MaxDiffValueLength } ? value[..MaxDiffValueLength] + "…" : value;

    private static ContentRevisionSummaryResponse ToSummary(ContentRevision revision, ContentItem item) =>
        ToSummary(new RevisionSummary(revision.Id, revision.Number, revision.Kind, revision.Note, revision.CreatedAtUtc, revision.CreatedById, revision.RestoredFromRevisionId), item);

    private static ContentRevisionSummaryResponse ToSummary(RevisionSummary revision, ContentItem item) =>
        new(revision.Id, revision.Number, revision.Kind.ToString(), revision.Note, revision.CreatedAtUtc, revision.CreatedById,
            revision.Id == item.LiveRevisionId && item.IsLive, revision.RestoredFromRevisionId);
}
