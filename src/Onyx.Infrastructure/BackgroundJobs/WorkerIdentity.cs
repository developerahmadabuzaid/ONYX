namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Unique lease owner for this Worker process. Leases are fenced on this value so a stale instance cannot complete work it no longer owns.</summary>
public sealed class WorkerIdentity
{
    public WorkerIdentity()
        : this($"{Environment.MachineName}/{Environment.ProcessId}/{Guid.NewGuid():N}")
    {
    }

    public WorkerIdentity(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        InstanceId = instanceId.Length > 200 ? instanceId[..200] : instanceId;
    }

    public string InstanceId { get; }
}
