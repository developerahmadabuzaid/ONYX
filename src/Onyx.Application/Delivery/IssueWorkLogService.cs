using System.Globalization;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Common;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Time logged on issues ("actual time spent"). Contributors log their own time; an entry is removed by the person who logged it
/// or by a project admin. Every change is recorded in the issue history.
/// </summary>
public sealed class IssueWorkLogService(
    IssueAccessLoader loader,
    IIssueRepository issues,
    IDeliveryReadModel readModel,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    private static readonly Error NotFound = Error.NotFound("Delivery.WorkLogNotFound", "Time entry not found.");

    public async Task<Result<WorkLogResponse>> AddAsync(Guid issueId, SaveWorkLogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await loader.LoadAsync(issueId, can => can.Comment, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (issue, _, viewer, _) = loaded.Value;
        var now = timeProvider.GetUtcNow();
        var created = IssueWorkLog.Create(issue, viewer.UserId, request.Minutes, request.WorkDate, request.Note, DateOnly.FromDateTime(now.UtcDateTime), now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var log = created.Value;
        issues.AddWorkLog(log);
        issues.AddHistory(new IssueHistoryEntry(issue.ProjectId, issue.Id, viewer.UserId, "worklog", null, log.Minutes.ToString(CultureInfo.InvariantCulture), now));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var names = await readModel.DisplayNamesAsync([viewer.UserId], cancellationToken).ConfigureAwait(false);
        return new WorkLogResponse(log.Id, log.UserId, names.GetValueOrDefault(log.UserId, string.Empty), log.Minutes, log.WorkDate, log.Note, log.CreatedAtUtc, CanDelete: true);
    }

    public async Task<Result> DeleteAsync(Guid issueId, Guid workLogId, CancellationToken cancellationToken)
    {
        var loaded = await loader.LoadAsync(issueId, _ => true, cancellationToken).ConfigureAwait(false);
        if (loaded.IsFailure)
        {
            return loaded.Error!;
        }

        var (issue, _, viewer, can) = loaded.Value;
        var log = await issues.GetWorkLogAsync(workLogId, cancellationToken).ConfigureAwait(false);
        if (log is null || log.IssueId != issueId)
        {
            return NotFound;
        }

        if (!(log.UserId == viewer.UserId && can.Comment) && !can.ManageProject)
        {
            return Error.Forbidden("Delivery.NotWorkLogOwner", "Only the person who logged the time or a project admin can remove it.");
        }

        issues.RemoveWorkLog(log);
        issues.AddHistory(new IssueHistoryEntry(
            issue.ProjectId, issue.Id, viewer.UserId, "worklog", log.Minutes.ToString(CultureInfo.InvariantCulture), null, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
