using Onyx.Application.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Tests.Delivery;

/// <summary>The burndown is rebuilt from history exactly; the project health rule is stated, not estimated. Fictional sample data.</summary>
public sealed class BurndownCalculatorTests
{
    private static readonly Guid Sprint = Guid.CreateVersion7();
    private static readonly Guid Todo = Guid.CreateVersion7();
    private static readonly Guid Done = Guid.CreateVersion7();
    private static readonly DateOnly Day1 = new(2026, 11, 2);
    private static readonly IReadOnlySet<Guid> DoneStatuses = new HashSet<Guid> { Done };

    [Fact]
    public void Remaining_work_follows_completions_scope_changes_and_re_estimates()
    {
        var finishedEarly = Guid.CreateVersion7();
        var addedLater = Guid.CreateVersion7();
        var reEstimated = Guid.CreateVersion7();
        var issues = new[]
        {
            new SprintIssueState(finishedEarly, Sprint, Done, 5, At(Day1.AddDays(-3))),
            new SprintIssueState(addedLater, Sprint, Todo, 2, At(Day1.AddDays(-3))),
            new SprintIssueState(reEstimated, Sprint, Todo, 8, At(Day1.AddDays(-3))),
        };
        var events = new[]
        {
            new SprintHistoryEvent(finishedEarly, BurndownCalculator.StatusField, Todo.ToString(), Done.ToString(), At(Day1, 14)),
            new SprintHistoryEvent(addedLater, BurndownCalculator.SprintField, null, Sprint.ToString(), At(Day1.AddDays(1), 10)),
            new SprintHistoryEvent(reEstimated, BurndownCalculator.PointsField, "3", "8", At(Day1.AddDays(2), 9)),
        };

        var burndown = BurndownCalculator.Calculate(Sprint, Day1, Day1.AddDays(2), issues, events, DoneStatuses);

        Assert.Equal([3, 5, 10], burndown.Select(point => point.RemainingPoints));
        Assert.Equal([1, 2, 2], burndown.Select(point => point.RemainingIssues));
    }

    [Fact]
    public void Issues_moved_out_or_created_later_are_not_counted_before_they_belong()
    {
        var movedOut = Guid.CreateVersion7();
        var createdLater = Guid.CreateVersion7();
        var issues = new[]
        {
            new SprintIssueState(movedOut, null, Todo, 3, At(Day1.AddDays(-1))),
            new SprintIssueState(createdLater, Sprint, Todo, 1, At(Day1.AddDays(1), 12)),
        };
        var events = new[] { new SprintHistoryEvent(movedOut, BurndownCalculator.SprintField, Sprint.ToString(), null, At(Day1.AddDays(1), 8)) };

        var burndown = BurndownCalculator.Calculate(Sprint, Day1, Day1.AddDays(1), issues, events, DoneStatuses);

        Assert.Equal([3, 1], burndown.Select(point => point.RemainingPoints));
    }

    [Fact]
    public void A_closed_sprint_is_measured_at_the_moment_it_closed()
    {
        var unfinished = Guid.CreateVersion7();
        var closedAt = At(Day1, 16);
        var issues = new[] { new SprintIssueState(unfinished, null, Todo, 4, At(Day1.AddDays(-1))) };
        var events = new[] { new SprintHistoryEvent(unfinished, BurndownCalculator.SprintField, Sprint.ToString(), null, closedAt) };

        Assert.Equal(4, Assert.Single(BurndownCalculator.Calculate(Sprint, Day1, Day1, issues, events, DoneStatuses, closedAt)).RemainingPoints);
        Assert.Equal(0, Assert.Single(BurndownCalculator.Calculate(Sprint, Day1, Day1, issues, events, DoneStatuses)).RemainingPoints);
        Assert.Empty(BurndownCalculator.Calculate(Sprint, Day1, Day1.AddDays(-1), issues, events, DoneStatuses));
    }

    [Theory]
    [InlineData(DeliveryProjectStatus.Completed, -5, 3, ProjectHealth.Completed)]
    [InlineData(DeliveryProjectStatus.OnHold, -5, 3, ProjectHealth.OnHold)]
    [InlineData(DeliveryProjectStatus.Active, -1, 0, ProjectHealth.Delayed)]
    [InlineData(DeliveryProjectStatus.Active, 10, 2, ProjectHealth.AtRisk)]
    [InlineData(DeliveryProjectStatus.Planned, 10, 0, ProjectHealth.OnTrack)]
    public void Project_health_follows_the_stated_rule(DeliveryProjectStatus status, int targetOffset, int overdue, string expected) =>
        Assert.Equal(expected, ProjectHealth.Of(status, Day1.AddDays(targetOffset), overdue, Day1));

    private static DateTimeOffset At(DateOnly day, int hour = 9) => new(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);
}
