namespace Onyx.Application.Media;

/// <summary>Upload limits, bound from configuration by the host (<c>Media</c> section).</summary>
/// <param name="MaxFrames">Most frames an animated image may have (each frame is also held to the pixel limits).</param>
public sealed record MediaLimits(long MaxBytes, int MaxWidth, int MaxHeight, long MaxPixels, int MaxFrames = 1_000)
{
    public static MediaLimits Default { get; } = new(10 * 1024 * 1024, 8_000, 8_000, 40_000_000);
}
