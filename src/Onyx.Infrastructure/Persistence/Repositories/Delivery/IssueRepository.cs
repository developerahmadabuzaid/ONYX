using Microsoft.EntityFrameworkCore;
using Onyx.Application.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class IssueRepository(ApplicationDbContext dbContext) : IIssueRepository
{
    public Task<Issue?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssues.SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);

    public Task<Issue?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssues.SingleOrDefaultAsync(issue => issue.ProjectId == projectId && issue.Key == key, cancellationToken);

    public async Task<long> MaxRankAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryIssues.Where(issue => issue.ProjectId == projectId).MaxAsync(issue => (long?)issue.Rank, cancellationToken).ConfigureAwait(false) ?? 0;

    public async Task<IReadOnlyList<IssueRankSlot>> ColumnAsync(Guid projectId, Guid statusId, Guid? sprintId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryIssues.AsNoTracking()
            .Where(issue => issue.ProjectId == projectId && issue.StatusId == statusId && issue.Type != IssueType.Subtask)
            .Where(issue => sprintId == null || issue.SprintId == sprintId)
            .OrderBy(issue => issue.Rank)
            .Select(issue => new IssueRankSlot(issue.Id, issue.Rank))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<IssueRankSlot>> SprintListAsync(Guid projectId, Guid? sprintId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryIssues.AsNoTracking()
            .Where(issue => issue.ProjectId == projectId && issue.SprintId == sprintId && issue.Type != IssueType.Subtask)
            .OrderBy(issue => issue.Rank)
            .Select(issue => new IssueRankSlot(issue.Id, issue.Rank))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, long>> RenumberRanksAsync(Guid projectId, long spacing, CancellationToken cancellationToken)
    {
        var all = await dbContext.DeliveryIssues
            .Where(issue => issue.ProjectId == projectId)
            .OrderBy(issue => issue.Rank)
            .ThenBy(issue => issue.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var ranks = new Dictionary<Guid, long>(all.Count);
        for (var index = 0; index < all.Count; index++)
        {
            var rank = (index + 1) * spacing;
            all[index].SetRank(rank);
            ranks[all[index].Id] = rank;
        }

        return ranks;
    }

    public Task<bool> HasChildrenAsync(Guid issueId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssues.AnyAsync(issue => issue.ParentId == issueId || issue.EpicId == issueId, cancellationToken);

    public async Task<IReadOnlyList<Issue>> ListInSprintAsync(Guid sprintId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryIssues.Where(issue => issue.SprintId == sprintId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<Guid>> DoneStatusIdsAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await dbContext.DeliveryWorkflowStatuses.AsNoTracking()
            .Where(status => status.ProjectId == projectId && status.Category == StatusCategory.Done)
            .Select(status => status.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

    public Task<IssueComment?> GetCommentAsync(Guid commentId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueComments.SingleOrDefaultAsync(comment => comment.Id == commentId, cancellationToken);

    public Task<IssueLink?> GetLinkAsync(Guid linkId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueLinks.SingleOrDefaultAsync(link => link.Id == linkId, cancellationToken);

    public Task<bool> LinkExistsAsync(Guid sourceIssueId, Guid targetIssueId, IssueLinkType type, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueLinks.AnyAsync(link => link.SourceIssueId == sourceIssueId && link.TargetIssueId == targetIssueId && link.Type == type, cancellationToken);

    public Task<IssueWatcher?> GetWatcherAsync(Guid issueId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueWatchers.SingleOrDefaultAsync(watcher => watcher.IssueId == issueId && watcher.UserId == userId, cancellationToken);

    public void Add(Issue issue) => dbContext.DeliveryIssues.Add(issue);

    public void Remove(Issue issue) => dbContext.DeliveryIssues.Remove(issue);

    public void AddComment(IssueComment comment) => dbContext.DeliveryIssueComments.Add(comment);

    public void AddHistory(IssueHistoryEntry entry) => dbContext.DeliveryIssueHistory.Add(entry);

    public void AddLink(IssueLink link) => dbContext.DeliveryIssueLinks.Add(link);

    public void RemoveLink(IssueLink link) => dbContext.DeliveryIssueLinks.Remove(link);

    public void AddWatcher(IssueWatcher watcher) => dbContext.DeliveryIssueWatchers.Add(watcher);

    public void RemoveWatcher(IssueWatcher watcher) => dbContext.DeliveryIssueWatchers.Remove(watcher);

    public void SetExpectedVersion(Issue issue, uint version) =>
        dbContext.Entry(issue).Property(entry => entry.Version).OriginalValue = version;

    public Task<IssueAttachment?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueAttachments.SingleOrDefaultAsync(attachment => attachment.Id == attachmentId, cancellationToken);

    public Task<int> CountAttachmentsAsync(Guid issueId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueAttachments.CountAsync(attachment => attachment.IssueId == issueId, cancellationToken);

    public async Task<IReadOnlyList<string>> AttachmentKeysAsync(Guid issueId, CancellationToken cancellationToken) =>
        await dbContext.DeliveryIssueAttachments.AsNoTracking()
            .Where(attachment => attachment.IssueId == issueId)
            .Select(attachment => attachment.StorageKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void AddAttachment(IssueAttachment attachment) => dbContext.DeliveryIssueAttachments.Add(attachment);

    public void RemoveAttachment(IssueAttachment attachment) => dbContext.DeliveryIssueAttachments.Remove(attachment);

    public Task<IssueWorkLog?> GetWorkLogAsync(Guid workLogId, CancellationToken cancellationToken) =>
        dbContext.DeliveryIssueWorkLogs.SingleOrDefaultAsync(log => log.Id == workLogId, cancellationToken);

    public void AddWorkLog(IssueWorkLog workLog) => dbContext.DeliveryIssueWorkLogs.Add(workLog);

    public void RemoveWorkLog(IssueWorkLog workLog) => dbContext.DeliveryIssueWorkLogs.Remove(workLog);
}
