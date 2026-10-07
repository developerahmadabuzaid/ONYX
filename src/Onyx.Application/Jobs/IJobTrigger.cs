namespace Onyx.Application.Jobs;

/// <summary>
/// Asks the Worker to run a registered job at its next scheduler tick (the job itself runs under the normal lease). Used for
/// "sync now": Web and API never run provider calls themselves.
/// </summary>
public interface IJobTrigger
{
    /// <summary>False when the job is unknown to the Worker (not registered yet) or disabled.</summary>
    Task<bool> RequestRunAsync(string jobName, CancellationToken cancellationToken);
}
