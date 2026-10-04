using Microsoft.EntityFrameworkCore;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Email;
using Onyx.Application.Intelligence;
using Onyx.Application.Knowledge;
using Onyx.Application.Media;
using Onyx.Contracts.Common;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class MediaRepository(ApplicationDbContext dbContext) : IMediaRepository
{
    public Task<MediaAsset?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MediaAssets.SingleOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, MediaAsset>> GetLookupAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? new Dictionary<Guid, MediaAsset>()
            : await dbContext.MediaAssets.AsNoTracking().Where(asset => ids.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken).ConfigureAwait(false);

    public Task<MediaAsset?> FindBySha256Async(string sha256, CancellationToken cancellationToken) =>
        dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Sha256 == sha256, cancellationToken);

    public async Task<PagedResult<MediaAsset>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.MediaAssets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePattern.Contains(search.Trim());
            query = query.Where(asset => EF.Functions.ILike(asset.OriginalFileName, pattern, LikePattern.EscapeCharacter)
                || (asset.Title != null && EF.Functions.ILike(asset.Title, pattern, LikePattern.EscapeCharacter)));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.OrderByDescending(asset => asset.CreatedAtUtc).ThenBy(asset => asset.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<MediaAsset>(items, page, pageSize, total);
    }

    public void Add(MediaAsset asset) => dbContext.MediaAssets.Add(asset);

    public void Remove(MediaAsset asset) => dbContext.MediaAssets.Remove(asset);

    public void SetExpectedVersion(MediaAsset asset, uint version) => dbContext.Entry(asset).Property(entry => entry.Version).OriginalValue = version;

    public async Task<IReadOnlyList<MediaUsage>> ListUsagesAsync(Guid mediaAssetId, CancellationToken cancellationToken) =>
        await dbContext.MediaUsages.AsNoTracking().Where(usage => usage.MediaAssetId == mediaAssetId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, int>> CountUsagesAsync(IReadOnlyCollection<Guid> mediaAssetIds, CancellationToken cancellationToken) =>
        mediaAssetIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.MediaUsages.AsNoTracking().Where(usage => mediaAssetIds.Contains(usage.MediaAssetId))
                .GroupBy(usage => usage.MediaAssetId).Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken).ConfigureAwait(false);

    public async Task ReplaceUsagesAsync(Guid contentItemId, IReadOnlyList<MediaUsage> usages, CancellationToken cancellationToken)
    {
        // Diff against stored rows inside the unit of work (no immediate bulk delete), so the change commits with the content.
        var existing = await dbContext.MediaUsages.Where(usage => usage.ContentItemId == contentItemId).ToListAsync(cancellationToken).ConfigureAwait(false);
        static (Guid, string, MediaUsageContext, bool) Key(MediaUsage usage) => (usage.MediaAssetId, usage.LanguageCode, usage.Context, usage.IsLive);
        var wanted = usages.ToDictionary(Key);
        dbContext.MediaUsages.RemoveRange(existing.Where(usage => !wanted.ContainsKey(Key(usage))));
        var kept = existing.Select(Key).ToHashSet();
        dbContext.MediaUsages.AddRange(usages.Where(usage => !kept.Contains(Key(usage))));
    }

    public Task<bool> HasLiveUsageAsync(Guid mediaAssetId, CancellationToken cancellationToken) =>
        dbContext.MediaUsages.AnyAsync(usage => usage.MediaAssetId == mediaAssetId && usage.IsLive, cancellationToken);

    public Task<bool> IsUsedByWorkingCopyAsync(Guid mediaAssetId, Guid contentItemId, CancellationToken cancellationToken) =>
        dbContext.MediaUsages.AnyAsync(usage => usage.MediaAssetId == mediaAssetId && usage.ContentItemId == contentItemId && !usage.IsLive, cancellationToken);
}
