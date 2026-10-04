using Onyx.Application.Abstractions.Messaging;
using Onyx.Domain.Media;

namespace Onyx.Application.Media;

/// <summary>
/// Removes the stored file of a deleted media asset. Runs in the Worker from the outbox, so a storage failure is retried with
/// backoff and, if it keeps failing, dead-lettered with an administrator notification (the file stays tracked by that message).
/// Deleting a file that is already gone succeeds, so retries are safe.
/// </summary>
public sealed class MediaStorageCleanupHandler(IMediaStorage storage) : IDomainEventHandler<MediaAssetDeleted>
{
    public Task HandleAsync(MediaAssetDeleted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        return storage.DeleteAsync(domainEvent.StorageKey, cancellationToken);
    }
}
