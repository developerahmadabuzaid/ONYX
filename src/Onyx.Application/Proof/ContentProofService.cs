using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Contracts.Content;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Proof;

namespace Onyx.Application.Proof;

/// <summary>
/// Records who verified proof content, against which source and with which client permission (the API requires
/// <c>Proof.Verify</c>), and manages the three featured-work slots (the API requires <c>Projects.Publish</c>). Only live,
/// verified projects can be featured; the slot table makes the limit hold under concurrent requests.
/// </summary>
public sealed class ContentProofService(
    IContentRepository content,
    IContentPathRepository paths,
    IFeaturedProjectRepository featured,
    ContentAccess access,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IContentProofService
{
    public async Task<Result<ContentDetailResponse>> VerifyAsync(Guid id, VerifyProofRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<ClientPermission>(request.ClientPermission, out var permission))
        {
            return Error.Validation("Proof.InvalidPermission", "Client permission is NotApplicable or Granted.");
        }

        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, actor) = loaded.Value;
        content.SetExpectedVersion(item, request.Version);
        var verified = item.VerifyProof(actor.UserId, timeProvider.GetUtcNow(), request.SourceNote, permission, request.ClientPermissionNote);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        // Notes are not stored in the audit trail (they may name client contacts); who, when and the permission level are.
        auditTrail.Record(AuditAction.ProofVerified, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(),
            new { revision = item.CurrentRevisionNumber, clientPermission = permission.ToString() });
        return await SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> RevokeAsync(Guid id, ContentVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var item = loaded.Value.Item;
        content.SetExpectedVersion(item, request.Version);
        var revoked = item.RevokeProofVerification();
        if (revoked.IsFailure)
        {
            return revoked.Error!;
        }

        auditTrail.Record(AuditAction.ProofVerificationRevoked, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(), null);
        return await SaveAsync(item, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<ContentDetailResponse>> SetFeaturedAsync(Guid id, SetFeaturedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await access.ForReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (item, actor) = loaded.Value;
        if (item.Type != ContentType.Project)
        {
            return Error.Validation("Featured.NotProject", "Only projects can be featured.");
        }

        if (item.Version != request.Version)
        {
            return Error.Conflict("Concurrency.Conflict", "The record was changed by someone else. Reload and try again.");
        }

        var current = await featured.FindForContentAsync(item.Id, cancellationToken).ConfigureAwait(false);
        if (!request.Featured)
        {
            if (current is not null)
            {
                featured.Remove(current);
                auditTrail.Record(AuditAction.ProjectUnfeatured, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(), new { slot = current.Slot });
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            return await DetailAsync(item, null, cancellationToken).ConfigureAwait(false);
        }

        if (current is not null)
        {
            return await DetailAsync(item, current.Slot, cancellationToken).ConfigureAwait(false);
        }

        if (!item.IsLive || item.ProofVerification is null)
        {
            return Error.Validation("Featured.NotEligible", "Only live, verified projects can be featured.");
        }

        var taken = (await featured.ListAsync(cancellationToken).ConfigureAwait(false)).Select(slot => slot.Slot).ToHashSet();
        var free = Enumerable.Range(1, FeaturedProject.MaxFeatured).FirstOrDefault(slot => !taken.Contains(slot));
        if (free == 0)
        {
            return Error.Conflict("Featured.Full", $"The homepage shows at most {FeaturedProject.MaxFeatured} featured projects. Unfeature one first.");
        }

        var slot = FeaturedProject.Create(item.Id, free, actor.UserId, timeProvider.GetUtcNow());
        if (slot.IsFailure)
        {
            return slot.Error!;
        }

        featured.Add(slot.Value);
        auditTrail.Record(AuditAction.ProjectFeatured, AuditCategory.Publishing, nameof(ContentItem), item.Id.ToString(), new { slot = free });

        // A concurrent request that took the same slot (or featured the same project) fails on the unique indexes with a conflict.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(item, free, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ContentDetailResponse>> SaveAsync(ContentItem item, CancellationToken cancellationToken)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        var slot = item.Type == ContentType.Project ? (await featured.FindForContentAsync(item.Id, cancellationToken).ConfigureAwait(false))?.Slot : null;
        return await DetailAsync(item, slot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ContentDetailResponse>> DetailAsync(ContentItem item, int? slot, CancellationToken cancellationToken) =>
        ContentMapping.ToDetail(item, await paths.ListForItemAsync(item.Id, cancellationToken).ConfigureAwait(false), slot);
}
