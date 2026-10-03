namespace Onyx.Application.Jobs;

/// <summary>Result of a job run. Failures are signalled by throwing; <see cref="Skipped"/> is for "nothing to do / provider not configured".</summary>
public sealed record JobOutcome
{
    public const int MessageMaxLength = 500;

    private JobOutcome(bool wasSkipped, string? message)
    {
        WasSkipped = wasSkipped;
        Message = message is { Length: > MessageMaxLength } ? message[..MessageMaxLength] : message;
    }

    public bool WasSkipped { get; }

    public string? Message { get; }

    public static JobOutcome Succeeded(string? message = null) => new(false, message);

    public static JobOutcome Skipped(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new JobOutcome(true, reason);
    }
}
