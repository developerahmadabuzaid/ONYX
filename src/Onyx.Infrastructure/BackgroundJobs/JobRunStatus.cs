namespace Onyx.Infrastructure.BackgroundJobs;

public enum JobRunStatus
{
    Running = 0,
    Succeeded = 1,
    Skipped = 2,
    Failed = 3,
}
