using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

public interface IIssueRepository
{
    Task<Issue?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Issue?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken);

    Task<long> MaxRankAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Ranks of the issues in a status column (subtasks excluded); <paramref name="sprintId"/> limits it to one sprint.</summary>
    Task<IReadOnlyList<IssueRankSlot>> ColumnAsync(Guid projectId, Guid statusId, Guid? sprintId, CancellationToken cancellationToken);

    /// <summary>Ranks of the issues in one sprint, or in the backlog when <paramref name="sprintId"/> is null (subtasks excluded).</summary>
    Task<IReadOnlyList<IssueRankSlot>> SprintListAsync(Guid projectId, Guid? sprintId, CancellationToken cancellationToken);

    /// <summary>Rewrites every rank of the project with even spacing, keeping the current order (tracked; saved by the caller). Returns the new ranks.</summary>
    Task<IReadOnlyDictionary<Guid, long>> RenumberRanksAsync(Guid projectId, long spacing, CancellationToken cancellationToken);

    /// <summary>Whether subtasks or epic children point at this issue.</summary>
    Task<bool> HasChildrenAsync(Guid issueId, CancellationToken cancellationToken);

    /// <summary>Issues of the sprint, tracked (used when a sprint starts or closes).</summary>
    Task<IReadOnlyList<Issue>> ListInSprintAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>Done-category status ids of a project.</summary>
    Task<IReadOnlySet<Guid>> DoneStatusIdsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IssueComment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken);

    Task<IssueLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken);

    Task<bool> LinkExistsAsync(Guid sourceIssueId, Guid targetIssueId, IssueLinkType type, CancellationToken cancellationToken);

    Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId, CancellationToken cancellationToken);

    void Add(Issue issue);

    void Remove(Issue issue);

    void AddComment(IssueComment comment);

    void AddHistory(IssueHistoryEntry entry);

    void AddLink(IssueLink link);

    void RemoveLink(IssueLink link);

    void AddWatcher(IssueWatcher watcher);

    void RemoveWatcher(IssueWatcher watcher);

    void SetExpectedVersion(Issue issue, uint version);

    Task<IssueAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken);

    Task<int> CountAttachmentsAsync(Guid issueId, CancellationToken cancellationToken);

    /// <summary>Storage keys of an issue's attachments (their files are removed when the issue is deleted).</summary>
    Task<IReadOnlyList<string>> AttachmentKeysAsync(Guid issueId, CancellationToken cancellationToken);

    void AddAttachment(IssueAttachment attachment);

    void RemoveAttachment(IssueAttachment attachment);

    Task<IssueWorkLog?> GetWorkLogAsync(Guid workLogId, CancellationToken cancellationToken);

    void AddWorkLog(IssueWorkLog workLog);

    void RemoveWorkLog(IssueWorkLog workLog);
}
