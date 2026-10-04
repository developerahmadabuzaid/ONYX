using System.ComponentModel.DataAnnotations;

namespace Onyx.Infrastructure.Content;

/// <summary>Bound from <c>Content</c>.</summary>
public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>How long a draft preview link stays valid.</summary>
    [Range(5, 1_440)]
    public int PreviewTokenMinutes { get; set; } = 30;

    /// <summary>Maximum scheduled publications/unpublications handled per scheduler run.</summary>
    [Range(1, 500)]
    public int SchedulerBatchSize { get; set; } = 50;
}
