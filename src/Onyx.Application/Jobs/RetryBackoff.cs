namespace Onyx.Application.Jobs;

/// <summary>Exponential backoff for outbox and email retries: base × 2^(attempt-1), capped.</summary>
public static class RetryBackoff
{
    public static TimeSpan Delay(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        // Cap the exponent before multiplying so large attempt counts cannot overflow.
        var exponent = Math.Min(attempt - 1, 30);
        var delay = TimeSpan.FromTicks((long)Math.Min(baseDelay.Ticks * Math.Pow(2, exponent), maxDelay.Ticks));
        return delay < maxDelay ? delay : maxDelay;
    }
}
