namespace Onyx.Application.Abstractions.Messaging;

/// <summary>
/// Makes side-effecting event handlers idempotent under at-least-once outbox delivery. <see cref="TryBeginAsync"/> returns false
/// when the key was already handled; otherwise it records the key in the current unit of work, so the marker commits together
/// with the handler's own changes (emails, notifications). A unique key stops two concurrent deliveries from both committing.
/// </summary>
public interface IProcessedEventLog
{
    Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken);
}
