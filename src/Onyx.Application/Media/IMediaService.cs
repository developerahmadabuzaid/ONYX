using Onyx.Contracts.Common;
using Onyx.Contracts.Media;
using Onyx.Domain.Common;

namespace Onyx.Application.Media;

public interface IMediaService
{
    Task<Result<MediaAssetResponse>> UploadAsync(Stream content, string? fileName, CancellationToken cancellationToken);

    Task<PagedResult<MediaAssetResponse>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<MediaAssetResponse>> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<MediaUsageResponse>>> ListUsagesAsync(Guid id, CancellationToken cancellationToken);

    Task<Result<MediaAssetResponse>> UpdateTextsAsync(Guid id, UpdateMediaTextsRequest request, CancellationToken cancellationToken);

    /// <summary>Refused while any content (draft or live) or the Brand Kit uses the asset.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Public access: only assets used by live content, or by the working copy a valid preview token grants.</summary>
    Task<Result<MediaFile>> OpenPublicAsync(Guid id, string? previewToken, CancellationToken cancellationToken);

    Task<Result<MediaFile>> OpenAdminAsync(Guid id, CancellationToken cancellationToken);
}
