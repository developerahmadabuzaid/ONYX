namespace Onyx.Application.Abstractions.Persistence;

/// <summary>The data changed after the caller read it (optimistic concurrency). Surfaces as 409 so the editor can reload.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was changed by someone else. Reload and try again.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ConcurrencyConflictException(string message, string conflictCode, Exception innerException)
        : base(message, innerException)
    {
        ConflictCode = conflictCode;
    }

    /// <summary>
    /// Set when a database uniqueness rule (rather than a row version) rejected the change, so the service can return its own
    /// error (e.g. a slot booked by a concurrent request).
    /// </summary>
    public string? ConflictCode { get; }
}
