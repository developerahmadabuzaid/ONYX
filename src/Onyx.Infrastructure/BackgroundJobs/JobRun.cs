namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>History of a single job execution, shown on the Admin system-jobs screen.</summary>
public sealed class JobRun
{
    public const int MessageMaxLength = 500;

    private JobRun()
    {
        JobName = string.Empty;
        WorkerId = string.Empty;
    }

    public Guid Id { get; private set; }

    public string JobName { get; private set; }

    public string WorkerId { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public JobRunStatus Status { get; private set; }

    public string? Message { get; private set; }

    public static JobRun Start(string jobName, string workerId, DateTimeOffset startedAtUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        JobName = jobName,
        WorkerId = workerId,
        StartedAtUtc = startedAtUtc,
        Status = JobRunStatus.Running,
    };

    public void Finish(JobRunStatus status, string? message, DateTimeOffset finishedAtUtc)
    {
        Status = status;
        Message = message is { Length: > MessageMaxLength } ? message[..MessageMaxLength] : message;
        FinishedAtUtc = finishedAtUtc;
    }
}
