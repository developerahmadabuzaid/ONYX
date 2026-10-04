using System.Globalization;
using System.Security.Cryptography;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Brand;
using Onyx.Application.Content;
using Onyx.Application.Localization;
using Onyx.Contracts.Common;
using Onyx.Contracts.Media;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Media;

namespace Onyx.Application.Media;

/// <summary>
/// Media library. Uploads are identified from their bytes, size- and dimension-limited, stripped of embedded metadata and
/// stored under random keys; identical files are stored once. Deletion is refused while anything uses the asset.
/// </summary>
public sealed class MediaService(
    IMediaRepository media,
    IMediaStorage storage,
    IImageInspector inspector,
    IContentRepository content,
    IBrandKitRepository brandKits,
    IContentPreviewService previews,
    Seo.IOrganizationProfileService organization,
    MediaLimits limits,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IMediaService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("Media.NotFound", "Media not found.");

    public async Task<Result<MediaAssetResponse>> UploadAsync(Stream content, string? fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var buffered = await ReadBoundedAsync(content, limits.MaxBytes, cancellationToken).ConfigureAwait(false);
        if (buffered is null)
        {
            return Error.Validation("Media.TooLarge", $"Files are limited to {limits.MaxBytes / (1024 * 1024)} MB.");
        }

        if (buffered.Length == 0)
        {
            return Error.Validation("Media.Empty", "The file is empty.");
        }

        var inspected = inspector.InspectAndClean(buffered, limits);
        if (inspected.IsFailure)
        {
            return inspected.Error!;
        }

        var image = inspected.Value;
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(image.Content));
        if (await media.FindBySha256Async(sha256, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return await ToResponseAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        var now = timeProvider.GetUtcNow();
        var key = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}.{image.Extension}");
        var asset = MediaAsset.Create(key, fileName ?? "image", MediaKind.Image, image.ContentType, image.Extension, image.Content.LongLength, image.Width, image.Height, sha256);

        await storage.SaveAsync(key, image.Content, cancellationToken).ConfigureAwait(false);
        try
        {
            media.Add(asset);
            auditTrail.Record(AuditAction.MediaUploaded, AuditCategory.Content, nameof(MediaAsset), asset.Id.ToString(),
                new { asset.ContentType, asset.SizeBytes, asset.Width, asset.Height });
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return await ToResponseAsync(asset, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PagedResult<MediaAssetResponse>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var result = await media.ListAsync(search, Math.Max(page, 1), Math.Clamp(pageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        var counts = await media.CountUsagesAsync(result.Items.Select(asset => asset.Id).ToList(), cancellationToken).ConfigureAwait(false);
        return new PagedResult<MediaAssetResponse>(
            result.Items.Select(asset => ToResponse(asset, counts.GetValueOrDefault(asset.Id))).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<Result<MediaAssetResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await media.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return asset is null ? NotFound : await ToResponseAsync(asset, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<MediaUsageResponse>>> ListUsagesAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await media.GetAsync(id, cancellationToken).ConfigureAwait(false) is null)
        {
            return NotFound;
        }

        var usages = await media.ListUsagesAsync(id, cancellationToken).ConfigureAwait(false);
        var items = await content.GetLookupAsync(usages.Select(usage => usage.ContentItemId).Distinct().ToList(), cancellationToken).ConfigureAwait(false);
        return usages
            .Select(usage => new MediaUsageResponse(usage.ContentItemId, usage.LanguageCode, usage.Context.ToString(), usage.IsLive,
                items.TryGetValue(usage.ContentItemId, out var item) ? ContentMapping.Titles(item) : new Dictionary<string, string>()))
            .ToList();
    }

    public async Task<Result<MediaAssetResponse>> UpdateTextsAsync(Guid id, UpdateMediaTextsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((request.Texts ?? []).Any(text => !SupportedLanguages.All.Contains(text.LanguageCode)))
        {
            return Error.Validation("Media.UnsupportedLanguage", "Alt texts must use configured languages.");
        }

        var asset = await media.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return NotFound;
        }

        media.SetExpectedVersion(asset, request.Version);
        var updated = asset.UpdateTexts(request.Title, ContentMapping.ToDomain(request.Texts));
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ToResponseAsync(asset, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await media.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return NotFound;
        }

        var usages = await media.ListUsagesAsync(id, cancellationToken).ConfigureAwait(false);
        if (usages.Count > 0)
        {
            return Error.Conflict("Media.InUse",
                $"This file is used by {usages.Select(usage => usage.ContentItemId).Distinct().Count()} content item(s) ({usages.Count(usage => usage.IsLive)} live usage(s)). Remove it from that content first.");
        }

        if ((await brandKits.GetAsync(cancellationToken).ConfigureAwait(false))?.OfficialLogoMediaId == id)
        {
            return Error.Conflict("Media.BrandLogo", "This file is the official logo in the Brand Kit.");
        }

        media.Remove(asset);
        auditTrail.Record(AuditAction.MediaDeleted, AuditCategory.Content, nameof(MediaAsset), asset.Id.ToString(),
            new { asset.OriginalFileName, asset.ContentType, asset.SizeBytes, asset.Sha256 });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await storage.DeleteAsync(asset.StorageKey, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<MediaFile>> OpenPublicAsync(Guid id, string? previewToken, CancellationToken cancellationToken)
    {
        var asset = await media.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (asset is null)
        {
            return NotFound;
        }

        var allowed = await media.HasLiveUsageAsync(id, cancellationToken).ConfigureAwait(false)
            || (previewToken is not null && await previews.CanViewMediaAsync(previewToken, id, cancellationToken).ConfigureAwait(false))
            // The official logo is public only while verified Organization data references it.
            || (await organization.GetVerifiedAsync(cancellationToken).ConfigureAwait(false))?.LogoMediaId == id;
        return allowed ? await OpenAsync(asset, cancellationToken).ConfigureAwait(false) : NotFound;
    }

    public async Task<Result<MediaFile>> OpenAdminAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await media.GetAsync(id, cancellationToken).ConfigureAwait(false);
        return asset is null ? NotFound : await OpenAsync(asset, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<MediaFile>> OpenAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        var stream = await storage.OpenReadAsync(asset.StorageKey, cancellationToken).ConfigureAwait(false);
        return stream is null
            ? NotFound
            : new MediaFile(stream, asset.ContentType, $"{asset.Id:N}.{asset.Extension}", asset.SizeBytes, asset.Sha256);
    }

    private async Task<MediaAssetResponse> ToResponseAsync(MediaAsset asset, CancellationToken cancellationToken)
    {
        var counts = await media.CountUsagesAsync([asset.Id], cancellationToken).ConfigureAwait(false);
        return ToResponse(asset, counts.GetValueOrDefault(asset.Id));
    }

    private static MediaAssetResponse ToResponse(MediaAsset asset, int usageCount) => new(
        asset.Id, asset.OriginalFileName, asset.ContentType, asset.SizeBytes, asset.Width, asset.Height, asset.Title,
        asset.Texts.Select(ContentMapping.ToDto).ToList(), asset.CreatedAtUtc, usageCount, asset.Version);

    /// <summary>Reads at most <paramref name="maxBytes"/>; null when the stream is larger.</summary>
    private static async Task<byte[]?> ReadBoundedAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
