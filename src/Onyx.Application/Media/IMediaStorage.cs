namespace Onyx.Application.Media;

/// <summary>
/// Binary storage for media files, addressed by server-generated keys. The first adapter is the local file system; object
/// storage or a CDN origin can replace it without touching CMS or domain logic. Never exposes physical paths.
/// </summary>
public interface IMediaStorage
{
    Task SaveAsync(string storageKey, ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>Null when the key does not exist.</summary>
    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}
