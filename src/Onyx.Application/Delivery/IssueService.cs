using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Media;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Issue tracking: create, read, edit, status transitions (validated against the project workflow), board and backlog moves with
/// persistent ranking, comments, links and watchers. Every change is checked against the caller's project capability and recorded
/// in the issue history; each command is saved as one transaction.
/// </summary>
public sealed class IssueService(
    IDeliveryProjectRepository projects,
    IIssueRepository issues,
    ISprintRepository sprints,
    IDeliveryReadModel readModel,
    DeliveryAccess access,
    IMediaStorage storage,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>Most issues one bulk change may touch.</summary>
    public const int MaxBulkIssues = 100;

    /// <summary>The bulk actions <see cref="BulkAsync"/> understands.</summary>
    public static readonly IReadOnlySet<string> BulkActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Assign", "Priority", "AddLabel", "Delete" };

    private static readonly Error VersionRequired = Error.Validation("Delivery.VersionRequired", "Reload the issue and try again.");

    public async Task<Result<IssueDetailResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, capability: _ => true, cancellationToken).ConfigureAwait(false);
        return context.IsFailure ? context.Error! : await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> CreateAsync(SaveIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var project = request.ProjectId is { } projectId ? await projects.GetAsync(projectId, cancellationToken).ConfigureAwait(false) : null;
        if (project is null)
        {
            return DeliveryAccess.ProjectNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            return can.Error!;
        }

        if (!can.Value.CreateIssues)
        {
            return DeliveryAccess.Forbidden;
        }

        var type = DeliveryEnums.Parse<IssueType>(request.Type, "Type");
        if (type.IsFailure)
        {
            return type.Error!;
        }

        var details = ParseDetails(request, project);
        if (details.IsFailure)
        {
            return details.Error!;
        }

        var rank = await issues.MaxRankAsync(project.Id, cancellationToken).ConfigureAwait(false) + IssueRanking.Spacing;
        var created = Issue.Create(project, type.Value, viewer.UserId, details.Value, rank);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var issue = created.Value;
        var hierarchy = await ApplyHierarchyAsync(issue, request.ParentId, request.EpicId, cancellationToken).ConfigureAwait(false);
        if (hierarchy.IsFailure)
        {
            return hierarchy.Error!;
        }

        if (request.SprintId is { } sprintId)
        {
            var sprint = await OpenSprintAsync(project.Id, sprintId, cancellationToken).ConfigureAwait(false);
            if (sprint.IsFailure)
            {
                return sprint.Error!;
            }

            issue.SetSprint(sprintId);
        }

        var now = timeProvider.GetUtcNow();
        issues.Add(issue);
        issues.AddHistory(new IssueHistoryEntry(project.Id, issue.Id, viewer.UserId, "created", null, issue.Key, now));
        issues.AddWatcher(new IssueWatcher(issue.Id, viewer.UserId));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(new IssueContext(issue, project, viewer, can.Value), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> UpdateAsync(Guid id, SaveIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return VersionRequired;
        }

        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (issue, project, viewer, _) = context.Value;
        issues.SetExpectedVersion(issue, version);
        var details = ParseDetails(request, project);
        if (details.IsFailure)
        {
            return details.Error!;
        }

        var changes = new List<IssueChange>();
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = DeliveryEnums.Parse<IssueType>(request.Type, "Type");
            if (type.IsFailure)
            {
                return type.Error!;
            }

            var typeChange = issue.ChangeType(type.Value);
            if (typeChange.IsFailure)
            {
                return typeChange.Error!;
            }

            if (typeChange.Value is { } change)
            {
                changes.Add(change);
            }
        }

        var updated = issue.Update(details.Value);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        changes.AddRange(updated.Value);
        var parentBefore = issue.ParentId;
        var epicBefore = issue.EpicId;
        var hierarchy = await ApplyHierarchyAsync(issue, request.ParentId, request.EpicId, cancellationToken).ConfigureAwait(false);
        if (hierarchy.IsFailure)
        {
            return hierarchy.Error!;
        }

        if (parentBefore != issue.ParentId)
        {
            changes.Add(new IssueChange("parent", parentBefore?.ToString(), issue.ParentId?.ToString()));
        }

        if (epicBefore != issue.EpicId)
        {
            changes.Add(new IssueChange("epic", epicBefore?.ToString(), issue.EpicId?.ToString()));
        }

        if (request.SprintId != issue.SprintId)
        {
            if (request.SprintId is { } sprintId)
            {
                var sprint = await OpenSprintAsync(project.Id, sprintId, cancellationToken).ConfigureAwait(false);
                if (sprint.IsFailure)
                {
                    return sprint.Error!;
                }
            }

            if (issue.SetSprint(request.SprintId) is { } sprintChange)
            {
                changes.Add(sprintChange);
            }
        }

        RecordHistory(issue, viewer, changes);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> TransitionAsync(Guid id, TransitionIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await MoveAsync(id, new MoveIssueRequest(request.StatusId, false, null, null, request.Version), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A board or backlog move: optionally a new status (must be an allowed transition), optionally a new sprint (must be open), and a
    /// new position before <see cref="MoveIssueRequest.BeforeIssueId"/> in the list the issue lands in. Saved as one transaction.
    /// </summary>
    public async Task<Result<IssueDetailResponse>> MoveAsync(Guid id, MoveIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (issue, project, viewer, _) = context.Value;
        issues.SetExpectedVersion(issue, request.Version);
        var changes = new List<IssueChange>();
        var now = timeProvider.GetUtcNow();

        if (request.StatusId is { } statusId && statusId != issue.StatusId)
        {
            var status = project.FindStatus(statusId);
            if (status is null)
            {
                return Error.Validation("Delivery.StatusNotFound", "This status is not part of the project's workflow.");
            }

            if (!project.CanTransition(issue.StatusId, statusId))
            {
                return Error.Conflict("Delivery.TransitionNotAllowed", "The workflow does not allow this status change.");
            }

            if (issue.MoveTo(status, now) is { } statusChange)
            {
                changes.Add(statusChange);
            }
        }

        if (request.ChangeSprint && request.SprintId != issue.SprintId)
        {
            if (request.SprintId is { } sprintId)
            {
                var sprint = await OpenSprintAsync(project.Id, sprintId, cancellationToken).ConfigureAwait(false);
                if (sprint.IsFailure)
                {
                    return sprint.Error!;
                }
            }

            if (issue.SetSprint(request.SprintId) is { } sprintChange)
            {
                changes.Add(sprintChange);
            }
        }

        if (!issue.IsSubtask)
        {
            var placed = await PlaceAsync(issue, project.Id, request, cancellationToken).ConfigureAwait(false);
            if (placed.IsFailure)
            {
                return placed.Error!;
            }
        }

        RecordHistory(issue, viewer, changes);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, can => can.ManageProject, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var issue = context.Value.Issue;
        if (await issues.HasChildrenAsync(issue.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Delivery.IssueHasChildren", "Move or delete this issue's subtasks and epic children first.");
        }

        var attachmentKeys = await issues.AttachmentKeysAsync(issue.Id, cancellationToken).ConfigureAwait(false);
        issues.Remove(issue);
        auditTrail.Record(AuditAction.DeliveryIssueDeleted, AuditCategory.Delivery, nameof(Issue), issue.Id.ToString(), new { key = issue.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Attachment rows went with the issue (cascade); their files are removed once the deletion is saved.
        foreach (var key in attachmentKeys)
        {
            await storage.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <summary>Moves the start and due dates (calendar drag and drop, timeline). Needs the Developer role.</summary>
    public async Task<Result<IssueDetailResponse>> RescheduleAsync(Guid id, RescheduleIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (issue, _, viewer, _) = context.Value;
        issues.SetExpectedVersion(issue, request.Version);
        var changes = issue.Reschedule(request.StartDate, request.DueDate);
        if (changes.IsFailure)
        {
            return changes.Error!;
        }

        RecordHistory(issue, viewer, changes.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies one change to several issues. Each issue is authorized and saved on its own (the same rules as editing or deleting it
    /// one by one), so an issue the caller may not change is reported back without undoing the others.
    /// </summary>
    public async Task<Result<BulkIssueResultResponse>> BulkAsync(BulkIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ids = (request.IssueIds ?? []).Distinct().ToList();
        if (ids.Count is 0 or > MaxBulkIssues)
        {
            return Error.Validation("Delivery.BulkSelection", $"Select between 1 and {MaxBulkIssues} issues.");
        }

        if (request.Action is not { } action || !BulkActions.Contains(action))
        {
            return Error.Validation("Delivery.BulkAction", "Choose what to change.");
        }

        DeliveryPriority? priority = null;
        if (string.Equals(action, "Priority", StringComparison.OrdinalIgnoreCase))
        {
            var parsed = DeliveryEnums.Parse<DeliveryPriority>(request.Priority, "Priority");
            if (parsed.IsFailure)
            {
                return parsed.Error!;
            }

            priority = parsed.Value;
        }

        if (string.Equals(action, "AddLabel", StringComparison.OrdinalIgnoreCase))
        {
            var label = DeliveryText.Labels([request.Label ?? string.Empty]);
            if (label.IsFailure || label.Value.Count == 0)
            {
                return label.IsFailure ? label.Error! : Error.Validation("Delivery.BulkLabel", "Enter the label to add.");
            }
        }

        var succeeded = 0;
        var failed = new List<BulkIssueFailureResponse>();
        foreach (var id in ids)
        {
            var outcome = string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase)
                ? await DeleteForBulkAsync(id, cancellationToken).ConfigureAwait(false)
                : await EditForBulkAsync(id, action, request, priority, cancellationToken).ConfigureAwait(false);
            if (outcome.Error is { } error)
            {
                failed.Add(new BulkIssueFailureResponse(id, outcome.Key, error.Code, error.Message));
            }
            else
            {
                succeeded++;
            }
        }

        return new BulkIssueResultResponse(succeeded, failed);
    }

    private async Task<(string? Key, Error? Error)> DeleteForBulkAsync(Guid id, CancellationToken cancellationToken)
    {
        var key = (await issues.GetAsync(id, cancellationToken).ConfigureAwait(false))?.Key;
        var deleted = await DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return (key, deleted.IsFailure ? deleted.Error : null);
    }

    private async Task<(string? Key, Error? Error)> EditForBulkAsync(
        Guid id, string action, BulkIssueRequest request, DeliveryPriority? priority, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return (null, context.Error);
        }

        var (issue, project, viewer, _) = context.Value;
        var assignee = issue.AssigneeId;
        var labels = issue.Labels.ToList();
        var newPriority = issue.Priority;
        switch (action.ToUpperInvariant())
        {
            case "ASSIGN":
                if (request.AssigneeId is { } assigneeId && project.RoleOf(assigneeId) is null)
                {
                    return (issue.Key, Error.Validation("Delivery.AssigneeNotMember", "That person is not a member of this issue's project."));
                }

                assignee = request.AssigneeId;
                break;
            case "PRIORITY":
                newPriority = priority ?? issue.Priority;
                break;
            case "ADDLABEL":
                labels.Add(request.Label ?? string.Empty);
                break;
        }

        var details = new IssueDetails(
            issue.Title, issue.Description, newPriority, assignee, issue.StoryPoints, issue.EstimateHours, issue.StartDate, issue.DueDate,
            labels, issue.Components, issue.RepositoryUrl, issue.BranchName, issue.PullRequestUrl, issue.CommitUrl);
        var updated = issue.Update(details);
        if (updated.IsFailure)
        {
            return (issue.Key, updated.Error);
        }

        RecordHistory(issue, viewer, updated.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (issue.Key, null);
    }

    public async Task<Result<IssueDetailResponse>> AddCommentAsync(Guid id, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadAsync(id, can => can.Comment, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (issue, project, viewer, _) = context.Value;
        var parent = request.ParentCommentId is { } parentId ? await issues.GetCommentAsync(parentId, cancellationToken).ConfigureAwait(false) : null;
        if (request.ParentCommentId is not null && parent is null)
        {
            return Error.Validation("Delivery.InvalidReply", "The comment being answered no longer exists.");
        }

        var comment = IssueComment.Create(issue.Id, viewer.UserId, request.Body, parent);
        if (comment.IsFailure)
        {
            return comment.Error!;
        }

        issues.AddComment(comment.Value);
        issues.AddHistory(new IssueHistoryEntry(project.Id, issue.Id, viewer.UserId, "comment", null, null, timeProvider.GetUtcNow()));
        if (await issues.GetWatcherAsync(issue.Id, viewer.UserId, cancellationToken).ConfigureAwait(false) is null)
        {
            issues.AddWatcher(new IssueWatcher(issue.Id, viewer.UserId));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Authors edit their own comments.</summary>
    public async Task<Result<IssueDetailResponse>> EditCommentAsync(Guid id, Guid commentId, SaveCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadAsync(id, can => can.Comment, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var comment = await issues.GetCommentAsync(commentId, cancellationToken).ConfigureAwait(false);
        if (comment is null || comment.IssueId != id)
        {
            return Error.NotFound("Delivery.CommentNotFound", "Comment not found.");
        }

        if (comment.AuthorId != context.Value.Viewer.UserId)
        {
            return Error.Forbidden("Delivery.NotCommentAuthor", "Only the author can edit a comment.");
        }

        var edited = comment.Edit(request.Body, timeProvider.GetUtcNow());
        if (edited.IsFailure)
        {
            return edited.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Authors delete their own comments; project admins can delete any comment.</summary>
    public async Task<Result<IssueDetailResponse>> DeleteCommentAsync(Guid id, Guid commentId, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, can => can.Comment, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var comment = await issues.GetCommentAsync(commentId, cancellationToken).ConfigureAwait(false);
        if (comment is null || comment.IssueId != id)
        {
            return Error.NotFound("Delivery.CommentNotFound", "Comment not found.");
        }

        if (comment.AuthorId != context.Value.Viewer.UserId && !context.Value.Can.ManageProject)
        {
            return Error.Forbidden("Delivery.NotCommentAuthor", "Only the author or a project admin can delete a comment.");
        }

        comment.Delete();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> AddLinkAsync(Guid id, CreateIssueLinkRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var (issue, project, viewer, _) = context.Value;
        var type = DeliveryEnums.Parse<IssueLinkType>(request.Type, "Type");
        if (type.IsFailure)
        {
            return type.Error!;
        }

        var target = await issues.GetByKeyAsync(project.Id, request.TargetKey?.Trim().ToUpperInvariant() ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return Error.Validation("Delivery.LinkTargetNotFound", $"No issue with that key in {project.Key}.");
        }

        if (await issues.LinkExistsAsync(issue.Id, target.Id, type.Value, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Delivery.LinkExists", "These issues are already linked this way.");
        }

        var now = timeProvider.GetUtcNow();
        var link = IssueLink.Create(issue, target, type.Value, viewer.UserId, now);
        if (link.IsFailure)
        {
            return link.Error!;
        }

        issues.AddLink(link.Value);
        issues.AddHistory(new IssueHistoryEntry(project.Id, issue.Id, viewer.UserId, "link", null, $"{type.Value} {target.Key}", now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> RemoveLinkAsync(Guid id, Guid linkId, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, can => can.EditIssues, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var link = await issues.GetLinkAsync(linkId, cancellationToken).ConfigureAwait(false);
        if (link is null || (link.SourceIssueId != id && link.TargetIssueId != id))
        {
            return Error.NotFound("Delivery.LinkNotFound", "Link not found.");
        }

        issues.RemoveLink(link);
        issues.AddHistory(new IssueHistoryEntry(context.Value.Project.Id, id, context.Value.Viewer.UserId, "link", link.Type.ToString(), null, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IssueDetailResponse>> SetWatchingAsync(Guid id, bool watching, CancellationToken cancellationToken)
    {
        var context = await LoadAsync(id, _ => true, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var existing = await issues.GetWatcherAsync(id, context.Value.Viewer.UserId, cancellationToken).ConfigureAwait(false);
        if (watching && existing is null)
        {
            issues.AddWatcher(new IssueWatcher(id, context.Value.Viewer.UserId));
        }
        else if (!watching && existing is not null)
        {
            issues.RemoveWatcher(existing);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await DetailAsync(context.Value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> PlaceAsync(Issue issue, Guid projectId, MoveIssueRequest request, CancellationToken cancellationToken)
    {
        // Board moves rank within the status column (limited to the active sprint when the issue is in it); backlog moves rank
        // within the sprint or backlog list. Both use the project-wide rank, so the order is the same in every view.
        async Task<IReadOnlyList<IssueRankSlot>> ListAsync()
        {
            if (request.StatusId is not null && !request.ChangeSprint)
            {
                var activeSprint = await sprints.ActiveSprintIdAsync(projectId, cancellationToken).ConfigureAwait(false);
                var scope = activeSprint is { } active && issue.SprintId == active ? active : (Guid?)null;
                return await issues.ColumnAsync(projectId, issue.StatusId, scope, cancellationToken).ConfigureAwait(false);
            }

            return await issues.SprintListAsync(projectId, issue.SprintId, cancellationToken).ConfigureAwait(false);
        }

        var list = (await ListAsync().ConfigureAwait(false)).Where(slot => slot.IssueId != issue.Id).ToList();
        var (rank, notInList) = IssueRanking.Place(list, request.BeforeIssueId);
        if (notInList)
        {
            return Error.Validation("Delivery.InvalidPosition", "The issue to place this one before is not in the same list.");
        }

        if (rank is null)
        {
            var renumbered = await issues.RenumberRanksAsync(projectId, IssueRanking.Spacing, cancellationToken).ConfigureAwait(false);
            list = list.Select(slot => slot with { Rank = renumbered.GetValueOrDefault(slot.IssueId, slot.Rank) }).ToList();
            (rank, _) = IssueRanking.Place(list, request.BeforeIssueId);
        }

        issue.SetRank(rank ?? (list.Count == 0 ? IssueRanking.Spacing : list.Max(slot => slot.Rank) + IssueRanking.Spacing));
        return Result.Success();
    }

    private async Task<Result> ApplyHierarchyAsync(Issue issue, Guid? parentId, Guid? epicId, CancellationToken cancellationToken)
    {
        var parent = parentId is { } pid ? await issues.GetAsync(pid, cancellationToken).ConfigureAwait(false) : null;
        if (parentId is not null && parent is null)
        {
            return Error.Validation("Delivery.InvalidParent", "The parent issue was not found.");
        }

        var epic = epicId is { } eid ? await issues.GetAsync(eid, cancellationToken).ConfigureAwait(false) : null;
        if (epicId is not null && epic is null)
        {
            return Error.Validation("Delivery.InvalidEpic", "The epic was not found.");
        }

        return issue.SetHierarchy(parent, epic);
    }

    private async Task<Result<Sprint>> OpenSprintAsync(Guid projectId, Guid sprintId, CancellationToken cancellationToken)
    {
        var sprint = await sprints.GetAsync(sprintId, cancellationToken).ConfigureAwait(false);
        if (sprint is null || sprint.ProjectId != projectId)
        {
            return Error.Validation("Delivery.SprintNotFound", "The sprint was not found in this project.");
        }

        return sprint.IsOpen ? sprint : Error.Conflict("Delivery.SprintClosed", "Issues cannot be added to a completed or cancelled sprint.");
    }

    private void RecordHistory(Issue issue, DeliveryViewer viewer, IEnumerable<IssueChange> changes)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var change in changes)
        {
            // Long text fields record that they changed, not their full text.
            var (oldValue, newValue) = change.Field == "description" ? (null, null) : (change.OldValue, change.NewValue);
            issues.AddHistory(new IssueHistoryEntry(issue.ProjectId, issue.Id, viewer.UserId, change.Field, oldValue, newValue, now));
        }
    }

    private static Result<IssueDetails> ParseDetails(SaveIssueRequest request, DeliveryProject project)
    {
        var priority = DeliveryEnums.ParseOrDefault(request.Priority, "Priority", DeliveryPriority.Medium);
        if (priority.IsFailure)
        {
            return priority.Error!;
        }

        if (request.AssigneeId is { } assignee && project.RoleOf(assignee) is null)
        {
            return Error.Validation("Delivery.AssigneeNotMember", "Assign the issue to a member of the project.");
        }

        return new IssueDetails(
            request.Title, request.Description, priority.Value, request.AssigneeId, request.StoryPoints, request.EstimateHours,
            request.StartDate, request.DueDate, request.Labels, request.Components, request.RepositoryUrl, request.BranchName,
            request.PullRequestUrl, request.CommitUrl);
    }

    private async Task<Result<IssueContext>> LoadAsync(Guid issueId, Func<DeliveryCapabilities, bool> capability, CancellationToken cancellationToken)
    {
        var viewer = await access.ViewerAsync(cancellationToken).ConfigureAwait(false);
        var issue = await issues.GetAsync(issueId, cancellationToken).ConfigureAwait(false);
        if (issue is null)
        {
            return DeliveryAccess.IssueNotFound;
        }

        var project = await projects.GetAsync(issue.ProjectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            return DeliveryAccess.IssueNotFound;
        }

        var can = DeliveryAccess.For(viewer, project);
        if (can.IsFailure)
        {
            // Issues of projects the caller cannot see do not exist for them.
            return DeliveryAccess.IssueNotFound;
        }

        return capability(can.Value) ? new IssueContext(issue, project, viewer, can.Value) : DeliveryAccess.Forbidden;
    }

    private async Task<IssueDetailResponse> DetailAsync(IssueContext context, CancellationToken cancellationToken)
    {
        var (issue, project, viewer, can) = context;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var summary = await readModel.IssueAsync(issue.Id, today, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A saved issue must be readable.");
        var names = await readModel.DisplayNamesAsync([issue.ReporterId], cancellationToken).ConfigureAwait(false);
        var parent = issue.ParentId is { } parentId ? await readModel.IssueAsync(parentId, today, cancellationToken).ConfigureAwait(false) : null;
        var sprint = issue.SprintId is { } sprintId ? await readModel.SprintAsync(sprintId, cancellationToken).ConfigureAwait(false) : null;
        var (watchers, isWatching) = await readModel.WatchersAsync(issue.Id, viewer.UserId, cancellationToken).ConfigureAwait(false);
        var allowed = project.Statuses
            .Where(status => status.Id != issue.StatusId && project.CanTransition(issue.StatusId, status.Id))
            .OrderBy(status => status.Order)
            .Select(DeliveryProjectService.ToResponse)
            .ToList();

        var attachments = await readModel.AttachmentsAsync(issue.Id, viewer.UserId, can.ManageProject, cancellationToken).ConfigureAwait(false);
        var (workLogs, timeSpent) = await readModel.WorkLogsAsync(issue.Id, viewer.UserId, can.ManageProject, cancellationToken).ConfigureAwait(false);
        return new IssueDetailResponse(
            summary,
            issue.Description,
            names.GetValueOrDefault(issue.ReporterId, string.Empty),
            issue.ReporterId,
            issue.EstimateHours,
            issue.StartDate,
            issue.Components,
            parent?.Key,
            parent?.Title,
            sprint?.Name,
            issue.RepositoryUrl,
            issue.BranchName,
            issue.PullRequestUrl,
            issue.CommitUrl,
            issue.CreatedAtUtc,
            issue.ResolvedAtUtc,
            await readModel.ChildrenAsync(issue.Id, today, cancellationToken).ConfigureAwait(false),
            await readModel.LinksAsync(issue.Id, cancellationToken).ConfigureAwait(false),
            await readModel.CommentsAsync(issue.Id, viewer.UserId, cancellationToken).ConfigureAwait(false),
            await readModel.HistoryAsync(issue.Id, cancellationToken).ConfigureAwait(false),
            allowed,
            watchers,
            isWatching,
            can,
            attachments,
            workLogs,
            timeSpent);
    }

    private sealed record IssueContext(Issue Issue, DeliveryProject Project, DeliveryViewer Viewer, DeliveryCapabilities Can);
}
