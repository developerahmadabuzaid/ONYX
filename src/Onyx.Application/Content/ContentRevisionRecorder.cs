using Onyx.Application.Abstractions;
using Onyx.Domain.Content;

namespace Onyx.Application.Content;

/// <summary>Stores a revision of the working copy after every change. An unchanged snapshot is not stored twice.</summary>
public sealed class ContentRevisionRecorder(IContentRevisionRepository revisions, ICurrentUser currentUser, TimeProvider timeProvider)
{
    public async Task<ContentRevision> RecordAsync(ContentItem item, RevisionKind kind, string? note, Guid? restoredFrom, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        var json = ContentSnapshotSerializer.Serialize(ContentSnapshot.From(item));
        var hash = ContentSnapshotSerializer.Hash(json);

        if (kind is not (RevisionKind.Created or RevisionKind.Restored))
        {
            var latest = await revisions.GetLatestAsync(item.Id, cancellationToken).ConfigureAwait(false);
            if (latest is not null && latest.ContentHash == hash)
            {
                return latest;
            }
        }

        var revision = ContentRevision.Record(
            item.Id, item.RecordRevision(), kind, json, hash, note, restoredFrom, currentUser.UserId, timeProvider.GetUtcNow());
        revisions.Add(revision);
        return revision;
    }
}
