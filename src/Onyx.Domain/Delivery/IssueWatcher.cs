using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>A user following an issue.</summary>
public sealed class IssueWatcher : Entity
{
    private IssueWatcher()
    {
    }

    public IssueWatcher(Guid issueId, Guid userId)
    {
        IssueId = issueId;
        UserId = userId;
    }

    public Guid IssueId { get; private set; }

    public Guid UserId { get; private set; }
}
