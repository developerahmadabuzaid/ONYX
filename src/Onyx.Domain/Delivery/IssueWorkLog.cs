using Onyx.Domain.Common;

namespace Onyx.Domain.Delivery;

/// <summary>
/// Time someone spent on an issue ("actual time spent"). Entries are only added or removed, never edited, so the record of who
/// logged what stays honest. The issue's total is the sum of its entries.
/// </summary>
public sealed class IssueWorkLog : Entity
{
    public const int MaxMinutesPerEntry = 24 * 60;
    public const int NoteMaxLength = 500;

    private IssueWorkLog()
    {
    }

    public Guid ProjectId { get; private set; }

    public Guid IssueId { get; private set; }

    public Guid UserId { get; private set; }

    public int Minutes { get; private set; }

    /// <summary>The day the work was done (not the day it was logged).</summary>
    public DateOnly WorkDate { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <param name="today">Today's date; work cannot be logged for the future (one day of leeway covers time zones).</param>
    public static Result<IssueWorkLog> Create(Issue issue, Guid userId, int minutes, DateOnly workDate, string? note, DateOnly today, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (minutes is < 1 or > MaxMinutesPerEntry)
        {
            return Error.Validation("Delivery.InvalidWorkLogMinutes", "Log between 1 minute and 24 hours per entry.");
        }

        if (workDate > today.AddDays(1) || workDate < today.AddYears(-2))
        {
            return Error.Validation("Delivery.InvalidWorkLogDate", "The work date must be within the last two years and not in the future.");
        }

        var cleanNote = DeliveryText.Optional(note, NoteMaxLength, "Delivery.InvalidWorkLogNote", $"The note is at most {NoteMaxLength} characters.");
        if (cleanNote.IsFailure)
        {
            return cleanNote.Error!;
        }

        return new IssueWorkLog
        {
            ProjectId = issue.ProjectId,
            IssueId = issue.Id,
            UserId = userId,
            Minutes = minutes,
            WorkDate = workDate,
            Note = cleanNote.Value,
            CreatedAtUtc = now,
        };
    }
}
