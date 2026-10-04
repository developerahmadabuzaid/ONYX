using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Localization;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

public sealed class ContentAdministrationService(
    IContentRepository content,
    IContentRevisionRepository revisions,
    IContentPathRepository paths,
    IContentRelationRepository relations,
    IContentClusterRepository clusters,
    IRedirectRepository redirects,
    IMediaRepository media,
    ContentAccess access,
    ContentAuthorizer authorizer,
    ContentRevisionRecorder revisionRecorder,
    MediaUsageIndexer mediaUsage,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentAdministrationService
{
    public const int MaxPageSize = 100;

    private static readonly Error UnsupportedLanguage = Error.Validation("Content.UnsupportedLanguage", "Unsupported language.");

    public async Task<Result<ContentDetailResponse>> CreateAsync(CreateContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ContentType>(request.Type, out var type))
        {
            return Error.Validation("Content.InvalidType", "Unknown content type.");
        }

        var presentation = ContentPresentation.Page;
        if (!string.IsNullOrWhiteSpace(request.Presentation) && !EnumParsing.TryParse(request.Presentation, out presentation))
        {
            return Error.Validation("Content.InvalidPresentation", "Unknown presentation.");
        }

        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        if (ContentAuthorizer.CheckEdit(actor, type) is { } forbidden)
        {
            return forbidden;
        }

        if (await ValidateParentAsync(type, request.ParentId, null, cancellationToken).ConfigureAwait(false) is { } parentError)
        {
            return parentError;
        }

        var created = ContentItem.Create(type, request.ParentId, request.Key, presentation, request.SortOrder);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        content.Add(created.Value);
        await revisionRecorder.RecordAsync(created.Value, RevisionKind.Created, null, null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ContentMapping.ToDetail(created.Value, []);
    }

    public async Task<Result<ContentDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        return await DetailAsync(loaded.Value.Item, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PagedResult<ContentSummaryResponse>>> ListAsync(
        string? type, string? status, bool? isLive, bool needsUrlDecision, Guid? parentId, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        ContentType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!EnumParsing.TryParse<ContentType>(type, out var parsedType))
            {
                return Error.Validation("Content.InvalidType", "Unknown content type.");
            }

            typeFilter = parsedType;
        }

        ContentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!EnumParsing.TryParse<ContentStatus>(status, out var parsedStatus))
            {
                return Error.Validation("Content.InvalidStatus", "Unknown status.");
            }

            statusFilter = parsedStatus;
        }

        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var filter = new ContentListFilter(
            ContentAuthorizer.ReadableTypes(actor), typeFilter, statusFilter, isLive, needsUrlDecision, parentId,
            string.IsNullOrWhiteSpace(search) ? null : search.Trim()[..Math.Min(search.Trim().Length, 100)],
            Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize));
        var result = await content.ListAsync(filter, cancellationToken).ConfigureAwait(false);
        return new PagedResult<ContentSummaryResponse>(result.Items.Select(ContentMapping.ToSummary).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<ContentDetailResponse>> UpdateTranslationAsync(
        Guid id, string languageCode, UpdateContentTranslationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return UnsupportedLanguage;
        }

        var loaded = await access.ForEditAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        var body = ContentMapping.ToBlocks(request.Body);
        if (body.IsFailure)
        {
            return body.Error!;
        }

        var slug = Slug.Normalize(request.Slug);
        if (slug.Length == 0)
        {
            return Error.Validation("Content.InvalidSlug", "Enter a slug using letters or digits.");
        }

        if (await content.SlugTakenAsync(languageCode, ContentItem.BuildSlugScope(item.Type, item.ParentId), slug, item.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Content.SlugTaken", "Another item in the same section already uses this slug in this language.");
        }

        var updated = item.UpdateTranslation(languageCode, request.Title, request.Summary, slug, body.Value, ContentMapping.ToFaqs(request.Faqs));
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        return await SaveEditAsync(item, RevisionKind.Edited, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> UpdateSeoAsync(Guid id, string languageCode, UpdateContentSeoRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupportedLanguages.All.Contains(languageCode))
        {
            return UnsupportedLanguage;
        }

        var seo = ContentMapping.ToSeo(request.Seo);
        if (seo.IsFailure)
        {
            return seo.Error!;
        }

        var loaded = await access.ForEditAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var updated = loaded.Value.Item.UpdateSeo(languageCode, seo.Value);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        return await SaveEditAsync(loaded.Value.Item, RevisionKind.SeoUpdated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> UpdateSettingsAsync(Guid id, UpdateContentSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ContentPresentation>(request.Presentation, out var presentation))
        {
            return Error.Validation("Content.InvalidPresentation", "Unknown presentation.");
        }

        var loaded = await access.ForEditAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        if (await ValidateParentAsync(item.Type, request.ParentId, item.Id, cancellationToken).ConfigureAwait(false) is { } parentError)
        {
            return parentError;
        }

        if (item.IsLive && presentation == ContentPresentation.TaxonomyOnly)
        {
            return Error.Conflict("Content.LiveTaxonomy", "Take the page offline before turning it into a taxonomy-only term.");
        }

        var updated = item.UpdateSettings(request.ParentId, request.Key, presentation, request.SortOrder, request.PrimaryCtaId, request.FeaturedImageId);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        return await SaveEditAsync(item, RevisionKind.SettingsUpdated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeletePermanentAsync(Guid id, DeleteContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await access.ForEditAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        if (!string.Equals(request.Confirmation?.Trim(), item.Id.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("Content.ConfirmationRequired", "Type the content id to confirm permanent deletion.");
        }

        if (item.IsLive)
        {
            return Error.Conflict("Content.DeleteLive", "Unpublish or archive the content before deleting it permanently.");
        }

        if (await content.HasChildrenAsync(item.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Content.HasChildren", "Delete or move its child pages first.");
        }

        if (await content.IsOfflineRedirectTargetAsync(item.Id, cancellationToken).ConfigureAwait(false)
            || await redirects.TargetsContentAsync(item.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Content.IsRedirectTarget", "Other URLs redirect to this content. Point them elsewhere before deleting it.");
        }

        var itemPaths = await paths.ListForItemAsync(item.Id, cancellationToken).ConfigureAwait(false);
        if (itemPaths.Count > 0 && item.OfflineResolution is not (OfflineResolution.Gone or OfflineResolution.RedirectToContent))
        {
            return Error.Conflict("Content.UrlDecisionRequired",
                "This content had public URLs. Decide first whether they redirect to a relevant replacement or are gone (410).");
        }

        // The URLs outlive the item as explicit redirect rules, so deleting content never leaves published URLs unmanaged.
        foreach (var path in itemPaths)
        {
            if (await redirects.FromPathExistsAsync(path.LanguageCode, path.Path, null, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var rule = item.OfflineResolution == OfflineResolution.RedirectToContent
                ? Redirect.Create(path.LanguageCode, path.Path, RedirectKind.Permanent, item.OfflineRedirectToId, null, $"Former URL of deleted content {item.Id:D}")
                : Redirect.Create(path.LanguageCode, path.Path, RedirectKind.Gone, null, null, $"Former URL of deleted content {item.Id:D}");
            if (rule.IsFailure)
            {
                return rule.Error!;
            }

            redirects.Add(rule.Value);
            paths.Remove(path);
        }

        await media.ReplaceUsagesAsync(item.Id, [], cancellationToken).ConfigureAwait(false);
        foreach (var cluster in await clusters.ListContainingAsync(item.Id, cancellationToken).ConfigureAwait(false))
        {
            var tracked = await clusters.GetAsync(cluster.Id, cancellationToken).ConfigureAwait(false);
            tracked?.RemoveMember(item.Id);
        }

        auditTrail.Record(AuditAction.ContentDeletedPermanently, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(),
            new { type = item.Type.ToString(), titles = ContentMapping.Titles(item), revisions = item.CurrentRevisionNumber, urls = itemPaths.Count });
        content.Remove(item);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ContentRelationResponse>>> ListRelationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var list = await relations.ListFromAsync(id, cancellationToken).ConfigureAwait(false);
        var targets = await content.GetLookupAsync(list.Select(relation => relation.ToContentItemId).ToList(), cancellationToken).ConfigureAwait(false);
        return list
            .Select(relation => new ContentRelationResponse(
                relation.Id, relation.ToContentItemId, relation.Kind.ToString(), relation.Position, relation.Note,
                targets.TryGetValue(relation.ToContentItemId, out var target) ? ContentMapping.Titles(target) : new Dictionary<string, string>()))
            .ToList();
    }

    public async Task<Result<ContentRelationResponse>> AddRelationAsync(Guid id, AddContentRelationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<RelationKind>(request.Kind, out var kind))
        {
            return Error.Validation("Relation.InvalidKind", "Unknown relation kind.");
        }

        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        if (ContentAuthorizer.CheckEdit(loaded.Value.Actor, loaded.Value.Item.Type) is { } forbidden)
        {
            return forbidden;
        }

        var targets = await content.GetLookupAsync([request.ToContentItemId], cancellationToken).ConfigureAwait(false);
        if (!targets.TryGetValue(request.ToContentItemId, out var target))
        {
            return ContentErrors.NotFound;
        }

        if (await relations.ExistsAsync(id, request.ToContentItemId, kind, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Relation.Exists", "This relation already exists.");
        }

        var relation = ContentRelation.Create(id, request.ToContentItemId, kind, request.Position, request.Note, timeProvider.GetUtcNow());
        if (relation.IsFailure)
        {
            return relation.Error!;
        }

        relations.Add(relation.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ContentRelationResponse(relation.Value.Id, target.Id, kind.ToString(), relation.Value.Position, relation.Value.Note, ContentMapping.Titles(target));
    }

    public async Task<Result> RemoveRelationAsync(Guid id, Guid relationId, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        if (ContentAuthorizer.CheckEdit(loaded.Value.Actor, loaded.Value.Item.Type) is { } forbidden)
        {
            return forbidden;
        }

        var relation = await relations.GetAsync(relationId, cancellationToken).ConfigureAwait(false);
        if (relation is null || relation.FromContentItemId != id)
        {
            return Error.NotFound("Relation.NotFound", "Relation not found.");
        }

        relations.Remove(relation);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<RelatedSuggestionResponse>>> SuggestRelatedAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        var existing = (await relations.ListFromAsync(id, cancellationToken).ConfigureAwait(false)).Select(relation => relation.ToContentItemId).ToHashSet();
        var reasons = new Dictionary<Guid, string>();

        foreach (var cluster in await clusters.ListContainingAsync(id, cancellationToken).ConfigureAwait(false))
        {
            foreach (var member in cluster.Members.Where(member => member.ContentItemId != id))
            {
                reasons.TryAdd(member.ContentItemId, $"Same topic cluster: {cluster.Name}");
            }
        }

        if (item.ParentId is { } parentId)
        {
            reasons.TryAdd(parentId, "Parent page");
            foreach (var sibling in await content.ListChildrenAsync(parentId, cancellationToken).ConfigureAwait(false))
            {
                if (sibling.Id != id)
                {
                    reasons.TryAdd(sibling.Id, "Sibling under the same parent");
                }
            }
        }

        foreach (var child in await content.ListChildrenAsync(id, cancellationToken).ConfigureAwait(false))
        {
            reasons.TryAdd(child.Id, "Child page");
        }

        var candidates = reasons.Keys.Where(candidate => !existing.Contains(candidate)).ToList();
        var lookup = await content.GetLookupAsync(candidates, cancellationToken).ConfigureAwait(false);
        return candidates
            .Where(lookup.ContainsKey)
            .Take(20)
            .Select(candidate => new RelatedSuggestionResponse(candidate, reasons[candidate], ContentMapping.Titles(lookup[candidate])))
            .ToList();
    }

    private async Task<Result<ContentDetailResponse>> SaveEditAsync(ContentItem item, RevisionKind kind, CancellationToken cancellationToken)
    {
        await revisionRecorder.RecordAsync(item, kind, null, null, cancellationToken).ConfigureAwait(false);
        await mediaUsage.RebuildAsync(item.Id, ContentSnapshot.From(item), await LiveSnapshotAsync(item, cancellationToken).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ContentSnapshot?> LiveSnapshotAsync(ContentItem item, CancellationToken cancellationToken) =>
        item.LiveRevisionId is { } liveId && await revisions.GetAsync(liveId, cancellationToken).ConfigureAwait(false) is { } revision
            ? ContentSnapshotSerializer.Deserialize(revision.Snapshot)
            : null;

    private async Task<ContentDetailResponse> DetailAsync(ContentItem item, CancellationToken cancellationToken) =>
        ContentMapping.ToDetail(item, await paths.ListForItemAsync(item.Id, cancellationToken).ConfigureAwait(false));

    private async Task<Error?> ValidateParentAsync(ContentType type, Guid? parentId, Guid? itemId, CancellationToken cancellationToken)
    {
        if (parentId is not { } parent)
        {
            return null;
        }

        var parents = await content.GetLookupAsync([parent], cancellationToken).ConfigureAwait(false);
        if (!parents.TryGetValue(parent, out var parentItem))
        {
            return Error.Validation("Content.InvalidParent", "The parent does not exist.");
        }

        var allowed = type switch
        {
            ContentType.Service => parentItem.Type is ContentType.ServicePillar or ContentType.Service,
            ContentType.Page => parentItem.Type == ContentType.Page,
            _ => false,
        };
        if (!allowed)
        {
            return Error.Validation("Content.InvalidParent", $"A {type} cannot be placed under a {parentItem.Type}.");
        }

        // Walk up from the new parent: refuse cycles (an item under its own descendant) and count the parent's level.
        // The walk is bounded by the supported depth; a longer chain is itself too deep.
        var parentLevel = 0;
        var current = parentItem;
        while (current is not null)
        {
            if (current.Id == itemId)
            {
                return Error.Validation("Content.InvalidParent", "Content cannot be moved under one of its own descendants.");
            }

            if (++parentLevel >= ContentItem.MaxHierarchyDepth)
            {
                return ContentErrors.HierarchyTooDeep;
            }

            current = current.ParentId is { } next
                ? (await content.GetLookupAsync([next], cancellationToken).ConfigureAwait(false)).GetValueOrDefault(next)
                : null;
        }

        // The item and everything below it move together, so the whole subtree must still fit.
        var subtreeHeight = itemId is { } id ? await SubtreeHeightAsync(id, ContentItem.MaxHierarchyDepth - parentLevel, cancellationToken).ConfigureAwait(false) : 1;
        return parentLevel + subtreeHeight > ContentItem.MaxHierarchyDepth ? ContentErrors.HierarchyTooDeep : null;
    }

    /// <summary>Levels in the subtree rooted at <paramref name="itemId"/> (1 = no children); stops counting once past <paramref name="limit"/>.</summary>
    private async Task<int> SubtreeHeightAsync(Guid itemId, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return 1;
        }

        var height = 1;
        foreach (var child in await content.ListChildrenAsync(itemId, cancellationToken).ConfigureAwait(false))
        {
            height = Math.Max(height, 1 + await SubtreeHeightAsync(child.Id, limit - 1, cancellationToken).ConfigureAwait(false));
            if (height > limit)
            {
                break;
            }
        }

        return height;
    }
}
