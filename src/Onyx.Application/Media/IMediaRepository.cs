using Onyx.Contracts.Common;
using Onyx.Domain.Media;

namespace Onyx.Application.Media;

public interface IMediaRepository
{
    Task<MediaAsset?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, MediaAsset>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<MediaAsset?> FindBySha256Async(string sha256, CancellationToken cancellationToken);

    Task<PagedResult<MediaAsset>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    void Add(MediaAsset asset);

    void Remove(MediaAsset asset);

    void SetExpectedVersion(MediaAsset asset, uint version);

    Task<IReadOnlyList<MediaUsage>> ListUsagesAsync(Guid mediaAssetId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> CountUsagesAsync(IReadOnlyCollection<Guid> mediaAssetIds, CancellationToken cancellationToken);

    /// <summary>Replaces every usage row recorded for the content item.</summary>
    Task ReplaceUsagesAsync(Guid contentItemId, IReadOnlyList<MediaUsage> usages, CancellationToken cancellationToken);

    Task<bool> HasLiveUsageAsync(Guid mediaAssetId, CancellationToken cancellationToken);

    Task<bool> IsUsedByWorkingCopyAsync(Guid mediaAssetId, Guid contentItemId, CancellationToken cancellationToken);
}
