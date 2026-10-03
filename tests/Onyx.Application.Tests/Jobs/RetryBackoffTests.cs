using Onyx.Application.Jobs;

namespace Onyx.Application.Tests.Jobs;

public sealed class RetryBackoffTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Max = TimeSpan.FromMinutes(60);

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    public void Delay_doubles_per_attempt(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RetryBackoff.Delay(attempt, Base, Max));
    }

    [Theory]
    [InlineData(8)]
    [InlineData(50)]
    [InlineData(int.MaxValue)]
    public void Delay_is_capped(int attempt)
    {
        Assert.Equal(Max, RetryBackoff.Delay(attempt, Base, Max));
    }

    [Fact]
    public void Attempts_start_at_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryBackoff.Delay(0, Base, Max));
    }
}
