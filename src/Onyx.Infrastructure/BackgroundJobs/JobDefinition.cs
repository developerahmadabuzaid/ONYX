namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Persistent schedule and lease for one code-defined <see cref="Application.Jobs.IScheduledJob"/>.</summary>
public sealed class JobDefinition
{
    public const int NameMaxLength = 100;
    public const int ScheduleMaxLength = 50;

    private JobDefinition()
    {
        Name = string.Empty;
        Schedule = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string Schedule { get; private set; }

    public bool IsEnabled { get; private set; }

    public DateTimeOffset NextRunAtUtc { get; private set; }

    public DateTimeOffset? LastRunAtUtc { get; private set; }

    public JobRunStatus? LastStatus { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
