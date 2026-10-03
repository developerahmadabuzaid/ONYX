using Onyx.Application.Jobs;

namespace Onyx.Application.Tests.Jobs;

public sealed class JobScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 10, 15, 30, TimeSpan.Zero);

    [Theory]
    [InlineData("every:30s", 30)]
    [InlineData("every:5m", 300)]
    [InlineData("every:2h", 7_200)]
    public void Interval_schedules_add_the_interval(string expression, int seconds)
    {
        var schedule = JobSchedule.Parse(expression);

        Assert.Equal(Now.AddSeconds(seconds), schedule.GetNextOccurrence(Now));
    }

    [Fact]
    public void Daily_schedule_runs_later_today_when_the_time_has_not_passed()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 22, 0, 0, TimeSpan.Zero), JobSchedule.Parse("daily@22:00").GetNextOccurrence(Now));
    }

    [Fact]
    public void Daily_schedule_runs_tomorrow_when_the_time_has_passed()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 3, 0, 0, TimeSpan.Zero), JobSchedule.Parse("daily@03:00").GetNextOccurrence(Now));
    }

    [Fact]
    public void Daily_schedule_is_strictly_after_the_reference_time()
    {
        var exactly = new DateTimeOffset(2026, 10, 4, 3, 0, 0, TimeSpan.Zero);

        Assert.Equal(exactly.AddDays(1), JobSchedule.Parse("daily@03:00").GetNextOccurrence(exactly));
    }

    [Fact]
    public void Non_utc_reference_times_are_normalized()
    {
        var riyadh = new DateTimeOffset(2026, 10, 4, 13, 15, 30, TimeSpan.FromHours(3));

        Assert.Equal(new DateTimeOffset(2026, 10, 4, 22, 0, 0, TimeSpan.Zero), JobSchedule.Parse("daily@22:00").GetNextOccurrence(riyadh));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("every:5s")]
    [InlineData("every:0m")]
    [InlineData("every:5d")]
    [InlineData("every:200h")]
    [InlineData("daily@24:00")]
    [InlineData("daily@3:00")]
    [InlineData("0 3 * * *")]
    [InlineData("every:5m; DROP TABLE job_definitions")]
    public void Invalid_expressions_are_rejected(string? expression)
    {
        Assert.False(JobSchedule.TryParse(expression, out _));
    }

    [Fact]
    public void Parse_throws_for_invalid_expressions()
    {
        Assert.Throws<FormatException>(() => JobSchedule.Parse("hourly"));
    }
}
