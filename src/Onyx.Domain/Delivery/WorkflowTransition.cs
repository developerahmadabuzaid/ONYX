using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>An allowed move between two statuses of the same project. Any move not listed is rejected by the server.</summary>
public sealed class WorkflowTransition : Entity
{
    private WorkflowTransition()
    {
    }

    internal WorkflowTransition(Guid projectId, Guid fromStatusId, Guid toStatusId)
    {
        ProjectId = projectId;
        FromStatusId = fromStatusId;
        ToStatusId = toStatusId;
    }

    public Guid ProjectId { get; private set; }

    public Guid FromStatusId { get; private set; }

    public Guid ToStatusId { get; private set; }
}
