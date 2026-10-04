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
}
