using Onyx.Application.Auditing;
using Onyx.Application.Localization;
using Onyx.Application.Media;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// The single path content takes to go live or offline, shared by editors and the Worker scheduler: quality gate → domain
/// transition → URL paths (with redirects for old URLs) → media usage → audit. Callers commit the unit of work.
/// </summary>
public sealed class ContentPublisher(
    ContentQualityGate qualityGate,
    ContentPathService pathService,
    IContentRepository content,
    IContentRevisionRepository revisions,
    MediaUsageIndexer mediaUsage,
    IAuditTrail auditTrail,
    TimeProvider timeProvider)
{
    public static readonly Error HasLiveChildren =
        Error.Conflict("Content.HasLiveChildren", "Take its live child pages offline first; their URLs and breadcrumbs depend on this page.");

    public async Task<Result> PublishAsync(ContentItem item, bool scheduled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var report = await qualityGate.EvaluateAsync(item, cancellationToken).ConfigureAwait(false);
        if (!report.CanPublish)
        {
            return report.ToError();
        }

        if (item.ApprovedRevisionId is not { } approvedId || await revisions.GetAsync(approvedId, cancellationToken).ConfigureAwait(false) is not { } approved)
        {
            return ContentErrors.InvalidTransition(item.Status, "publish");
        }

        var snapshot = ContentSnapshotSerializer.Deserialize(approved.Snapshot);
        var newPaths = await pathService.ComputeAsync(item, snapshot, cancellationToken).ConfigureAwait(false);
        if (newPaths.IsFailure)
        {
            return newPaths.Error!;
        }

        var now = timeProvider.GetUtcNow();
        var transition = scheduled ? item.PublishScheduled(now, SupportedLanguages.All) : item.Publish(now, SupportedLanguages.All);
        if (transition.IsFailure)
        {
            return transition;
        }

        var applied = await pathService.ApplyAsync(item.Id, newPaths.Value, cancellationToken).ConfigureAwait(false);
        if (applied.IsFailure)
        {
            return applied;
        }

        var descendants = await pathService.RefreshDescendantsAsync(item.Id, cancellationToken).ConfigureAwait(false);
        if (descendants.IsFailure)
        {
            return descendants;
        }

        await mediaUsage.RebuildAsync(item.Id, ContentSnapshot.From(item), snapshot, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.ContentPublished, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(),
            new { revision = approved.Number, scheduled, paths = newPaths.Value });
        return Result.Success();
    }

    /// <summary>Unpublishes (or archives) the item. A redirect target must be different, live content.</summary>
    public async Task<Result> TakeOfflineAsync(
        ContentItem item, OfflineResolution resolution, Guid? redirectToId, bool archive, bool scheduled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsLive && (await content.ListChildrenAsync(item.Id, cancellationToken).ConfigureAwait(false)).Any(child => child.IsLive))
        {
            return HasLiveChildren;
        }

        if (resolution == OfflineResolution.RedirectToContent && await ValidateTargetAsync(item.Id, redirectToId, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var wasLive = item.IsLive;
        var now = timeProvider.GetUtcNow();
        var transition = archive
            ? item.Archive(resolution, redirectToId, now)
            : scheduled ? item.UnpublishScheduled(now) : item.Unpublish(resolution, redirectToId);
        if (transition.IsFailure)
        {
            return transition;
        }

        await mediaUsage.RebuildAsync(item.Id, ContentSnapshot.From(item), null, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(archive ? AuditAction.ContentArchived : AuditAction.ContentUnpublished, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(),
            new { wasLive, resolution = item.OfflineResolution.ToString(), redirectTo = item.OfflineRedirectToId, scheduled });
        return Result.Success();
    }

    public async Task<Error?> ValidateTargetAsync(Guid itemId, Guid? targetId, CancellationToken cancellationToken)
    {
        if (targetId is not { } target || target == itemId)
        {
            return ContentErrors.InvalidResolution;
        }

        var targets = await content.GetLookupAsync([target], cancellationToken).ConfigureAwait(false);
        return targets.TryGetValue(target, out var targetItem) && targetItem.IsLive
            ? null
            : Error.Validation("Content.RedirectTargetNotLive", "Redirect only to content that is live, so visitors land on a real page.");
    }
}
