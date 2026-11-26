using System.Text.Json;
using FluentValidation;
using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Media;
using Onyx.Contracts.SiteContent;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Media;
using Onyx.Domain.SiteContent;

namespace Onyx.Application.SiteContent;

public sealed class SiteContentService(
    ISiteContentRepository documents,
    IMediaRepository media,
    IValidator<SaveSiteContentDraftRequest> draftValidator,
    IValidator<SiteSettingsContent> settingsValidator,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ISiteContentService
{
    public const int MaxRevisions = 25;

    private static readonly JsonSerializerOptions SettingsJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly Error UnknownKey = Error.NotFound("SiteContent.NotFound", "Unknown site content document.");

    public async Task<IReadOnlyList<SiteContentDocumentSummaryResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await documents.ListAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(document => document.Key, StringComparer.Ordinal)
            .Select(document => new SiteContentDocumentSummaryResponse(
                document.Key, document.HasDraft, document.PublishedJson is not null, document.DraftUpdatedAtUtc, document.PublishedAtUtc, document.PublishedRevision))
            .ToList();

    public async Task<Result<SiteContentDocumentResponse>> GetAsync(string key, CancellationToken cancellationToken)
    {
        if (!SiteContentDocument.IsValidKey(key))
        {
            return UnknownKey;
        }

        var document = await documents.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return document is null ? new SiteContentDocumentResponse(key, null, null, null, null, null, null, 0, 0) : ToResponse(document);
    }

    public async Task<Result<SiteContentDocumentResponse>> SaveDraftAsync(string key, SaveSiteContentDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SiteContentDocument.IsValidKey(key))
        {
            return UnknownKey;
        }

        if (await draftValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        if (SiteContentJson.Validate(request.Json, out var mediaIds) is { } problem)
        {
            return Error.Validation("SiteContent.Invalid", problem);
        }

        if (key == SiteContentDocument.SettingsKey && await ValidateSettingsAsync(request.Json, cancellationToken).ConfigureAwait(false) is { } settingsError)
        {
            return settingsError;
        }

        if (await ValidateMediaAsync(mediaIds, cancellationToken).ConfigureAwait(false) is { } mediaError)
        {
            return mediaError;
        }

        var document = await documents.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            var created = SiteContentDocument.Create(key);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            document = created.Value!;
            documents.Add(document);
        }
        else
        {
            if (request.Version is not { } version)
            {
                return Error.Conflict("SiteContent.Version", "This document already exists; reload it and save again.");
            }

            documents.SetExpectedVersion(document, version);
        }

        var saved = document.SaveDraft(request.Json, mediaIds, timeProvider.GetUtcNow(), currentUser.UserId);
        if (saved.IsFailure)
        {
            return saved.Error!;
        }

        auditTrail.Record(AuditAction.SiteContentDraftSaved, AuditCategory.Content, nameof(SiteContentDocument), document.Id.ToString(),
            new { document.Key, length = request.Json.Length, media = mediaIds.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(document);
    }

    public async Task<Result<SiteContentDocumentResponse>> PublishAsync(string key, SiteContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await FindAsync(key, request.Version, cancellationToken).ConfigureAwait(false);
        if (document.IsFailure)
        {
            return document.Error!;
        }

        var target = document.Value!;

        // Media may have been deleted since the draft was saved; never publish a reference to a missing file.
        if (await ValidateMediaAsync(target.DraftMediaIds, cancellationToken).ConfigureAwait(false) is { } mediaError)
        {
            return mediaError;
        }

        var revision = target.Publish(timeProvider.GetUtcNow(), currentUser.UserId);
        if (revision.IsFailure)
        {
            return revision.Error!;
        }

        documents.AddRevision(revision.Value!);
        auditTrail.Record(AuditAction.SiteContentPublished, AuditCategory.Publishing, nameof(SiteContentDocument), target.Id.ToString(),
            new { target.Key, revision = target.PublishedRevision, media = target.PublishedMediaIds.Count });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(target);
    }

    public async Task<Result<SiteContentDocumentResponse>> DiscardDraftAsync(string key, SiteContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await FindAsync(key, request.Version, cancellationToken).ConfigureAwait(false);
        if (document.IsFailure)
        {
            return document.Error!;
        }

        var target = document.Value!;
        var discarded = target.DiscardDraft();
        if (discarded.IsFailure)
        {
            return discarded.Error!;
        }

        auditTrail.Record(AuditAction.SiteContentDraftDiscarded, AuditCategory.Content, nameof(SiteContentDocument), target.Id.ToString(), new { target.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(target);
    }

    public async Task<Result<IReadOnlyList<SiteContentRevisionResponse>>> ListRevisionsAsync(string key, CancellationToken cancellationToken)
    {
        if (!SiteContentDocument.IsValidKey(key))
        {
            return UnknownKey;
        }

        var document = await documents.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return Result.Success<IReadOnlyList<SiteContentRevisionResponse>>([]);
        }

        var revisions = await documents.ListRevisionsAsync(document.Id, MaxRevisions, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SiteContentRevisionResponse>>(
            revisions.Select(revision => new SiteContentRevisionResponse(revision.Number, revision.PublishedAtUtc, revision.PublishedById, revision.Json)).ToList());
    }

    public async Task<Result<SiteContentDocumentResponse>> RestoreRevisionAsync(string key, int number, SiteContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = await FindAsync(key, request.Version, cancellationToken).ConfigureAwait(false);
        if (document.IsFailure)
        {
            return document.Error!;
        }

        var target = document.Value!;
        var revision = await documents.GetRevisionAsync(target.Id, number, cancellationToken).ConfigureAwait(false);
        if (revision is null)
        {
            return Error.NotFound("SiteContent.RevisionNotFound", "Revision not found.");
        }

        if (await ValidateMediaAsync(revision.MediaIds, cancellationToken).ConfigureAwait(false) is { } mediaError)
        {
            return mediaError;
        }

        var restored = target.RestoreToDraft(revision, timeProvider.GetUtcNow(), currentUser.UserId);
        if (restored.IsFailure)
        {
            return restored.Error!;
        }

        auditTrail.Record(AuditAction.SiteContentRevisionRestored, AuditCategory.Content, nameof(SiteContentDocument), target.Id.ToString(), new { target.Key, revision = number });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(target);
    }

    public async Task<PublicSiteContentResponse> GetPublishedAsync(CancellationToken cancellationToken) =>
        new(await documents.ListPublishedAsync(cancellationToken).ConfigureAwait(false));

    private async Task<Result<SiteContentDocument>> FindAsync(string key, uint version, CancellationToken cancellationToken)
    {
        if (!SiteContentDocument.IsValidKey(key))
        {
            return UnknownKey;
        }

        var document = await documents.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return UnknownKey;
        }

        documents.SetExpectedVersion(document, version);
        return document;
    }

    private async Task<Error?> ValidateSettingsAsync(string json, CancellationToken cancellationToken)
    {
        SiteSettingsContent? settings;
        try
        {
            settings = JsonSerializer.Deserialize<SiteSettingsContent>(json, SettingsJson);
        }
        catch (JsonException)
        {
            return Error.Validation("SiteContent.Settings", "The settings document does not have the expected shape.");
        }

        return settings is null
            ? Error.Validation("SiteContent.Settings", "The settings document is empty.")
            : await settingsValidator.ValidateToErrorAsync(settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Error?> ValidateMediaAsync(IReadOnlyCollection<Guid> mediaIds, CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
        {
            return null;
        }

        var found = await media.GetLookupAsync(mediaIds, cancellationToken).ConfigureAwait(false);
        var missing = mediaIds.Where(id => !found.TryGetValue(id, out var asset) || asset.Kind != MediaKind.Image).ToList();
        return missing.Count == 0
            ? null
            : Error.Validation("SiteContent.Media", $"These images are not in the media library: {string.Join(", ", missing)}.");
    }

    private static SiteContentDocumentResponse ToResponse(SiteContentDocument document) => new(
        document.Key,
        document.DraftJson,
        document.PublishedJson,
        document.DraftUpdatedAtUtc,
        document.DraftUpdatedById,
        document.PublishedAtUtc,
        document.PublishedById,
        document.PublishedRevision,
        document.Version);
}
