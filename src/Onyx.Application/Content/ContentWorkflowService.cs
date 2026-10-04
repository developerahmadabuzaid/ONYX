using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Intelligence;
using Onyx.Application.Localization;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>
/// Lifecycle transitions. Approval and publishing are separate permissions (enforced by the API) and separate steps; every
/// transition is audited. Publishing always goes through <see cref="ContentPublisher"/> and its quality gate.
/// </summary>
public sealed class ContentWorkflowService(
    IContentPathRepository paths,
    ContentAccess access,
    ContentRevisionRecorder revisionRecorder,
    ContentQualityGate qualityGate,
    ContentPublisher publisher,
    IPublishingTimeAdvisor publishingTimeAdvisor,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentWorkflowService
{
    public async Task<Result<ContentDetailResponse>> SubmitForReviewAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, async (item, actor) =>
        {
            var revision = await revisionRecorder.RecordAsync(item, RevisionKind.Edited, null, null, cancellationToken).ConfigureAwait(false);
            var result = item.SubmitForReview(actor.UserId, revision.Id, timeProvider.GetUtcNow());
            return Audited(result, AuditAction.ContentSubmittedForReview, item, new { revision = revision.Number });
        }, cancellationToken).ConfigureAwait(false);

    public async Task<Result<ContentDetailResponse>> ApproveAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, async (item, actor) =>
        {
            var report = await qualityGate.EvaluateAsync(item, cancellationToken).ConfigureAwait(false);
            if (!report.CanPublish)
            {
                return report.ToError();
            }

            var result = item.Approve(actor.UserId, timeProvider.GetUtcNow(), SupportedLanguages.All);
            return Audited(result, AuditAction.ContentApproved, item, null);
        }, cancellationToken).ConfigureAwait(false);

    public async Task<Result<ContentDetailResponse>> RejectAsync(Guid id, RejectContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await TransitionAsync(id, request.Version, (item, _) =>
            Task.FromResult(Audited(item.Reject(request.Note), AuditAction.ContentRejected, item, new { note = item.ReviewNote })), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> PublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, (item, _) => publisher.PublishAsync(item, scheduled: false, cancellationToken), cancellationToken).ConfigureAwait(false);

    public async Task<Result<ContentDetailResponse>> ScheduleAsync(Guid id, ScheduleContentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TimeZone is { } zone && !IsKnownTimeZone(zone))
        {
            return Error.Validation("Content.InvalidTimeZone", "Unknown time zone.");
        }

        return await TransitionAsync(id, request.Version, async (item, _) =>
        {
            var report = await qualityGate.EvaluateAsync(item, cancellationToken).ConfigureAwait(false);
            if (!report.CanPublish)
            {
                return report.ToError();
            }

            var result = item.Schedule(request.PublishAt, request.TimeZone, timeProvider.GetUtcNow());
            return Audited(result, AuditAction.ContentScheduled, item, new { publishAtUtc = item.ScheduledPublishAtUtc, timeZone = item.ScheduleTimeZone });
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> CancelScheduleAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, (item, _) =>
            Task.FromResult(Audited(item.CancelSchedule(), AuditAction.ContentScheduleCancelled, item, null)), cancellationToken).ConfigureAwait(false);

    public async Task<Result<ContentDetailResponse>> ScheduleUnpublishAsync(Guid id, ScheduleUnpublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await TransitionAsync(id, request.Version, (item, _) =>
            Task.FromResult(Audited(item.ScheduleUnpublish(request.UnpublishAt, timeProvider.GetUtcNow()), AuditAction.ContentUnpublishScheduled, item,
                new { unpublishAtUtc = item.ScheduledUnpublishAtUtc })), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> CancelUnpublishScheduleAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, (item, _) =>
            Task.FromResult(Audited(item.CancelUnpublishSchedule(), AuditAction.ContentScheduleCancelled, item, new { unpublish = true })), cancellationToken).ConfigureAwait(false);

    public Task<Result<ContentDetailResponse>> UnpublishAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken) =>
        TakeOfflineAsync(id, request, archive: false, cancellationToken);

    public Task<Result<ContentDetailResponse>> ArchiveAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken) =>
        TakeOfflineAsync(id, request, archive: true, cancellationToken);

    public async Task<Result<ContentDetailResponse>> RestoreFromArchiveAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken) =>
        await TransitionAsync(id, request?.Version ?? 0, (item, _) =>
            Task.FromResult(Audited(item.RestoreFromArchive(), AuditAction.ContentRestoredFromArchive, item, null)), cancellationToken).ConfigureAwait(false);

    public async Task<Result<ContentDetailResponse>> ResolveOfflineAsync(Guid id, TakeOfflineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<OfflineResolution>(request.Resolution, out var resolution))
        {
            return ContentErrors.InvalidResolution;
        }

        return await TransitionAsync(id, request.Version, async (item, _) =>
        {
            if (resolution == OfflineResolution.RedirectToContent
                && await publisher.ValidateTargetAsync(item.Id, request.RedirectToContentItemId, cancellationToken).ConfigureAwait(false) is { } invalid)
            {
                return invalid;
            }

            var result = item.ResolveOffline(resolution, request.RedirectToContentItemId);
            return Audited(result, AuditAction.ContentUrlResolutionDecided, item, new { resolution = resolution.ToString(), redirectTo = request.RedirectToContentItemId });
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<QualityReportResponse>> GetQualityReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        return (await qualityGate.EvaluateAsync(loaded.Value.Item, cancellationToken).ConfigureAwait(false)).ToResponse();
    }

    public async Task<Result<PublishingTimeRecommendationResponse>> RecommendPublishingTimeAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        return await publishingTimeAdvisor.RecommendAsync(loaded.Value.Item, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ContentDetailResponse>> TakeOfflineAsync(Guid id, TakeOfflineRequest request, bool archive, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<OfflineResolution>(request.Resolution, out var resolution) || resolution == OfflineResolution.None)
        {
            return ContentErrors.InvalidResolution;
        }

        return await TransitionAsync(id, request.Version, (item, _) =>
            publisher.TakeOfflineAsync(item, resolution, request.RedirectToContentItemId, archive, scheduled: false, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ContentDetailResponse>> TransitionAsync(
        Guid id, uint version, Func<ContentItem, Identity.ActorPermissions, Task<Result>> transition, CancellationToken cancellationToken)
    {
        var loaded = await access.ForEditAsync(id, version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, actor) = loaded.Value;
        var result = await transition(item, actor).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ContentMapping.ToDetail(item, await paths.ListForItemAsync(item.Id, cancellationToken).ConfigureAwait(false));
    }

    private Result Audited(Result result, AuditAction action, ContentItem item, object? details)
    {
        if (result.IsSuccess)
        {
            auditTrail.Record(action, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(), details);
        }

        return result;
    }

    private static bool IsKnownTimeZone(string zone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
            return zone.Length <= ContentItem.TimeZoneMaxLength;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
