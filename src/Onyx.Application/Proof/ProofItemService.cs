using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Application.Identity;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Proof;

namespace Onyx.Application.Proof;

/// <summary>
/// Proof items belong to a project or case study, so every operation needs that content type's scope. Testimonials also need
/// the <c>Testimonials.*</c> permission for the action; publishing other items needs <c>Content.Publish</c>; verification needs
/// <c>Proof.Verify</c> (checked by the API). An item is public only when verified and published, and only while its page is live.
/// Publishing or removing a visible item changes the page, so its <c>lastmod</c> moves and its URLs are announced.
/// </summary>
public sealed class ProofItemService(
    IProofItemRepository items,
    IContentRepository content,
    ContentAuthorizer authorizer,
    ContentPublisher publisher,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IProofItemService
{
    private static readonly Error NotFound = Error.NotFound("Proof.NotFound", "Proof item not found.");

    private enum Action
    {
        View,
        Create,
        Edit,
        Publish,
        Delete,
    }

    public async Task<Result<IReadOnlyList<ProofItemResponse>>> ListAsync(Guid contentItemId, CancellationToken cancellationToken)
    {
        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var parent = await content.GetAsync(contentItemId, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return ContentErrors.NotFound;
        }

        if (ContentAuthorizer.CheckRead(actor, parent.Type) is { } forbidden)
        {
            return forbidden;
        }

        return (await items.ListForContentAsync(contentItemId, cancellationToken).ConfigureAwait(false))
            .Where(item => Allowed(actor, item.Kind, Action.View))
            .Select(ToResponse)
            .ToList();
    }

    public async Task<Result<ProofItemResponse>> CreateAsync(SaveProofItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ProofItemKind>(request.Kind, out var kind))
        {
            return Error.Validation("Proof.InvalidKind", "Kind is Capability, Metric or Testimonial.");
        }

        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var parent = await content.GetAsync(request.ContentItemId, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return ContentErrors.NotFound;
        }

        if (parent.Type is not (ContentType.Project or ContentType.CaseStudy))
        {
            return Error.Validation("Proof.InvalidParent", "Capabilities, metrics and testimonials belong to a project or case study.");
        }

        if (ContentAuthorizer.CheckEdit(actor, parent.Type) is not null || !Allowed(actor, kind, Action.Create))
        {
            return ContentAuthorizer.TypeForbidden;
        }

        var created = ProofItem.Create(
            parent.Id, kind, ContentMapping.ToDomain(request.Texts), request.Value, request.AsOf, request.AttributionName, request.AttributionRole,
            request.AttributionOrganization, request.SortOrder);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        items.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(created.Value);
    }

    public async Task<Result<ProofItemResponse>> UpdateAsync(Guid id, SaveProofItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(id, Action.Edit, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, parent) = loaded.Value;
        if (request.ContentItemId != item.ContentItemId || !string.Equals(request.Kind, item.Kind.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Error.Validation("Proof.Immutable", "The page and kind of a proof item cannot change; create a new item instead.");
        }

        var wasPublic = item.Status == ProofItemStatus.Published;
        var updated = item.Update(
            ContentMapping.ToDomain(request.Texts), request.Value, request.AsOf, request.AttributionName, request.AttributionRole, request.AttributionOrganization,
            request.SortOrder);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        // An edit takes the item off the page until it is verified and published again.
        if (wasPublic)
        {
            await PageChangedAsync(parent, item, cancellationToken).ConfigureAwait(false);
            auditTrail.Record(AuditAction.ProofItemUnpublished, AuditCategory.Publishing, nameof(ProofItem), item.Id.ToString(), new { reason = "edited" });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(item);
    }

    public async Task<Result<ProofItemResponse>> VerifyAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ClientPermission>(request.ClientPermission, out var permission))
        {
            return Error.Validation("Proof.InvalidPermission", "Client permission is NotApplicable or Granted.");
        }

        var loaded = await LoadAsync(id, Action.View, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var verified = item.Verify(actor.UserId, timeProvider.GetUtcNow(), request.SourceNote, permission, request.ClientPermissionNote);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        auditTrail.Record(AuditAction.ProofVerified, AuditCategory.Publishing, nameof(ProofItem), item.Id.ToString(),
            new { kind = item.Kind.ToString(), clientPermission = permission.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(item);
    }

    public async Task<Result<ProofItemResponse>> PublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(id, Action.Publish, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, parent) = loaded.Value;
        var published = item.Publish(timeProvider.GetUtcNow());
        if (published.IsFailure)
        {
            return published.Error!;
        }

        await PageChangedAsync(parent, item, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.ProofItemPublished, AuditCategory.Publishing, nameof(ProofItem), item.Id.ToString(), new { kind = item.Kind.ToString(), page = parent.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(item);
    }

    public async Task<Result<ProofItemResponse>> UnpublishAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await LoadAsync(id, Action.Publish, request.Version, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, parent) = loaded.Value;
        var unpublished = item.Unpublish();
        if (unpublished.IsFailure)
        {
            return unpublished.Error!;
        }

        await PageChangedAsync(parent, item, cancellationToken).ConfigureAwait(false);
        auditTrail.Record(AuditAction.ProofItemUnpublished, AuditCategory.Publishing, nameof(ProofItem), item.Id.ToString(), new { kind = item.Kind.ToString(), page = parent.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(item);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(id, Action.Delete, null, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, parent) = loaded.Value;
        if (item.Status == ProofItemStatus.Published)
        {
            await PageChangedAsync(parent, item, cancellationToken).ConfigureAwait(false);
        }

        items.Remove(item);
        auditTrail.Record(AuditAction.ProofItemDeleted, AuditCategory.Content, nameof(ProofItem), item.Id.ToString(), new { kind = item.Kind.ToString(), page = parent.Id });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public static ProofItemResponse ToResponse(ProofItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ProofItemResponse(
            item.Id, item.ContentItemId, item.Kind.ToString(), item.Texts.Select(ContentMapping.ToDto).ToList(), item.Value, item.AsOf, item.AttributionName,
            item.AttributionRole, item.AttributionOrganization, item.SortOrder, item.Status.ToString(),
            item.Verification is { } verification ? ContentMapping.ToResponse(verification) : null, item.PublishedAtUtc, item.Version);
    }

    private static bool Allowed(ActorPermissions actor, ProofItemKind kind, Action action)
    {
        if (kind != ProofItemKind.Testimonial)
        {
            return action != Action.Publish || ContentAuthorizer.Has(actor, Permissions.Content.Publish);
        }

        return ContentAuthorizer.Has(actor, action switch
        {
            Action.View => Permissions.Testimonials.View,
            Action.Create => Permissions.Testimonials.Create,
            Action.Edit => Permissions.Testimonials.Edit,
            Action.Publish => Permissions.Testimonials.Publish,
            _ => Permissions.Testimonials.Delete,
        });
    }

    private async Task<Result<(ProofItem Item, ContentItem Parent)>> LoadAsync(Guid id, Action action, uint? version, CancellationToken cancellationToken)
    {
        var item = await items.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return NotFound;
        }

        var actor = await authorizer.ActorAsync(cancellationToken).ConfigureAwait(false);
        var parent = await content.GetAsync(item.ContentItemId, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return NotFound;
        }

        var scope = action == Action.View ? ContentAuthorizer.CheckRead(actor, parent.Type) : ContentAuthorizer.CheckEdit(actor, parent.Type);
        if (scope is not null || !Allowed(actor, item.Kind, action))
        {
            return ContentAuthorizer.TypeForbidden;
        }

        if (version is { } expected)
        {
            items.SetExpectedVersion(item, expected);
        }

        return (item, parent);
    }

    /// <summary>A visible proof item appeared or disappeared: the live page changed in the languages the item is written in.</summary>
    private async Task PageChangedAsync(ContentItem parent, ProofItem item, CancellationToken cancellationToken)
    {
        if (!parent.IsLive)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var text in item.Texts)
        {
            parent.RecordLiveContentModified(text.LanguageCode, now);
        }

        await publisher.AnnounceCurrentUrlsAsync(parent, cancellationToken).ConfigureAwait(false);
    }
}
