namespace Onyx.Application.Privacy;

/// <summary>
/// Retention job sizing, bound by the host (<c>Privacy</c> section). Technical limits only: retention periods are never set here,
/// they come from the policies an administrator saves.
/// </summary>
/// <param name="BatchSize">Records processed per transaction.</param>
/// <param name="MaxBatchesPerRun">Batches per category per run; the rest waits for the next run.</param>
public sealed record RetentionSettings(int BatchSize, int MaxBatchesPerRun)
{
    public static RetentionSettings Default { get; } = new(200, 50);
}
