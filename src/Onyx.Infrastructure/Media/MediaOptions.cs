using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Media;

/// <summary>Bound from <c>Media</c>. The storage root must be outside any web root and included in media backups.</summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Directory for the local file-system storage adapter; relative paths resolve against the host's content root.</summary>
    public string StorageRoot { get; set; } = string.Empty;

    [Range(1_024, 52_428_800)]
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;

    [Range(16, 20_000)]
    public int MaxWidth { get; set; } = 8_000;

    [Range(16, 20_000)]
    public int MaxHeight { get; set; } = 8_000;

    [Range(256, 200_000_000)]
    public long MaxPixels { get; set; } = 40_000_000;

    /// <summary>Most frames an animated GIF may contain.</summary>
    [Range(1, 10_000)]
    public int MaxFrames { get; set; } = 1_000;
}
