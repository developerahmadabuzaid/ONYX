namespace Onyx.Application.Content;

/// <summary>Content runtime settings, bound from configuration by the host (<c>Content</c> section).</summary>
public sealed record ContentSettings(TimeSpan PreviewTokenLifetime, int SchedulerBatchSize)
{
    public static ContentSettings Default { get; } = new(TimeSpan.FromMinutes(30), 50);
}
