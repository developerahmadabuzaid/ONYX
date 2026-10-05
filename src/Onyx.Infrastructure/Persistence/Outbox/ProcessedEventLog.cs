using Microsoft.EntityFrameworkCore;
using Onyx.Application.Abstractions.Messaging;

namespace Onyx.Infrastructure.Persistence.Outbox;

internal sealed class ProcessedEventLog(ApplicationDbContext dbContext, TimeProvider timeProvider) : IProcessedEventLog
{
    public async Task<bool> TryBeginAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (key.Length > ProcessedEvent.KeyMaxLength)
        {
            throw new ArgumentException("Processed-event keys are limited in length.", nameof(key));
        }

        if (await dbContext.ProcessedEvents.AnyAsync(processed => processed.Key == key, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        // Committed by the handler's own SaveChanges; a concurrent duplicate fails on the unique key and is retried (then skipped).
        dbContext.ProcessedEvents.Add(ProcessedEvent.Create(key, timeProvider.GetUtcNow()));
        return true;
    }
}
