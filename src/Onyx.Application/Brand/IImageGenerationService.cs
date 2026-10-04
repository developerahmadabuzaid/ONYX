using Onyx.Domain.Common;

namespace Onyx.Application.Brand;

/// <summary>
/// Port for a future AI image provider (none is connected or approved yet; nothing implements this). Generated images are
/// drafts for editor review: they never publish themselves.
/// </summary>
public interface IImageGenerationService
{
    Task<Result<GeneratedImage>> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken);
}
