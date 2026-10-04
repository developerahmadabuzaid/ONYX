using Onyx.Domain.Common;

namespace Onyx.Application.Media;

/// <summary>
/// Identifies an uploaded image from its bytes (never from its name or claimed type), checks limits and removes embedded
/// metadata (EXIF/XMP/text chunks, which can hold location data). Returns the cleaned bytes to store.
/// </summary>
public interface IImageInspector
{
    Result<InspectedImage> InspectAndClean(byte[] content, MediaLimits limits);
}
