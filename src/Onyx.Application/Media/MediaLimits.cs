namespace Onyx.Application.Media;

/// <summary>Upload limits, bound from configuration by the host (<c>Media</c> section).</summary>
public sealed record MediaLimits(long MaxBytes, int MaxWidth, int MaxHeight, long MaxPixels)
{
    public static MediaLimits Default { get; } = new(10 * 1024 * 1024, 8_000, 8_000, 40_000_000);
}
