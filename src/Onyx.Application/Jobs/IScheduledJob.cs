namespace Onyx.Application.Jobs;

/// <summary>
/// A recurring Worker job. Implementations must be idempotent (a job may re-run after a crash or lease expiry)
/// and delegate business work to application services.
/// </summary>
public interface IScheduledJob
{
    /// <summary>Stable unique name; persisted, so never rename an existing job.</summary>
    string Name { get; }

    /// <summary>Initial schedule (see <see cref="JobSchedule"/>), used when the job is first registered.</summary>
    string DefaultSchedule { get; }

    Task<JobOutcome> ExecuteAsync(CancellationToken cancellationToken);
}
