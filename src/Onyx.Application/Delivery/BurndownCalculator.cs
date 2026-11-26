using System.Globalization;
using Onyx.Contracts.Delivery;

namespace Onyx.Application.Delivery;

/// <summary>
/// Rebuilds a sprint burndown from the issue history. For every issue that is or ever was in the sprint, the state before its
/// first recorded change is derived from that change's old value; replaying the changes then gives its sprint, status and story
/// points at any moment. A day's value is the work in the sprint and not done at the end of that day (UTC), or at the moment the
/// sprint closed for its last day.
/// </summary>
public static class BurndownCalculator
{
    public const string SprintField = "sprint";
    public const string StatusField = "status";
    public const string PointsField = "storyPoints";

    public static IReadOnlyList<BurndownPointResponse> Calculate(
        Guid sprintId,
        DateOnly firstDay,
        DateOnly lastDay,
        IReadOnlyList<SprintIssueState> issues,
        IReadOnlyList<SprintHistoryEvent> events,
        IReadOnlySet<Guid> doneStatusIds,
        DateTimeOffset? closedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(doneStatusIds);
        if (lastDay < firstDay)
        {
            return [];
        }

        var timelines = issues.Select(issue => new IssueTimeline(
                issue,
                events.Where(change => change.IssueId == issue.IssueId).OrderBy(change => change.AtUtc).ToList()))
            .ToList();

        var points = new List<BurndownPointResponse>();
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            // A closed sprint is measured up to the moment it closed: unfinished work moved out at closing still counts as remaining.
            var endOfDay = new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            if (closedAtUtc is { } closed && closed < endOfDay)
            {
                endOfDay = closed;
            }

            var remainingPoints = 0;
            var remainingIssues = 0;
            foreach (var timeline in timelines)
            {
                if (timeline.State.CreatedAtUtc >= endOfDay)
                {
                    continue;
                }

                var (sprint, status, storyPoints) = timeline.At(endOfDay);
                if (sprint == sprintId && !doneStatusIds.Contains(status))
                {
                    remainingPoints += storyPoints ?? 0;
                    remainingIssues++;
                }
            }

            points.Add(new BurndownPointResponse(day, remainingPoints, remainingIssues));
        }

        return points;
    }

    private sealed class IssueTimeline(SprintIssueState state, IReadOnlyList<SprintHistoryEvent> changes)
    {
        private readonly Guid? _initialSprint = Initial(changes, SprintField, state.SprintId, ParseGuid);
        private readonly Guid _initialStatus = Initial(changes, StatusField, (Guid?)state.StatusId, ParseGuid) ?? state.StatusId;
        private readonly int? _initialPoints = Initial(changes, PointsField, state.StoryPoints, ParseInt);

        public SprintIssueState State { get; } = state;

        public (Guid? Sprint, Guid Status, int? Points) At(DateTimeOffset instant)
        {
            var sprint = _initialSprint;
            var status = _initialStatus;
            var points = _initialPoints;
            foreach (var change in changes)
            {
                if (change.AtUtc >= instant)
                {
                    break;
                }

                switch (change.Field)
                {
                    case SprintField:
                        sprint = ParseGuid(change.NewValue);
                        break;
                    case StatusField when ParseGuid(change.NewValue) is { } newStatus:
                        status = newStatus;
                        break;
                    case PointsField:
                        points = ParseInt(change.NewValue);
                        break;
                }
            }

            return (sprint, status, points);
        }

        // Before the first recorded change of a field, the field held that change's old value; with no change, it is unchanged.
        private static T? Initial<T>(IReadOnlyList<SprintHistoryEvent> changes, string field, T? current, Func<string?, T?> parse)
            where T : struct
        {
            var first = changes.FirstOrDefault(change => change.Field == field);
            return first is null ? current : parse(first.OldValue);
        }

        private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var id) ? id : null;

        private static int? ParseInt(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}
