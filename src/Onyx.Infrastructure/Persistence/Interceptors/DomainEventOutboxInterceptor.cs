using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Converts raised domain events into <see cref="OutboxMessage"/> rows in the same transaction (no dual write).
/// Events are cleared from entities only after a successful save, so a failed save can be retried without losing or duplicating them.
/// </summary>
public sealed class DomainEventOutboxInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly List<OutboxMessage> _pendingMessages = [];
    private readonly List<IHasDomainEvents> _pendingSources = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        CompletePending();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        CompletePending();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        DiscardPending(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        DiscardPending(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var sources = context.ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        foreach (var source in sources)
        {
            foreach (var domainEvent in source.DomainEvents)
            {
                var message = OutboxMessage.FromDomainEvent(domainEvent, now, SerializerOptions);
                context.Set<OutboxMessage>().Add(message);
                _pendingMessages.Add(message);
            }

            _pendingSources.Add(source);
        }
    }

    private void CompletePending()
    {
        foreach (var source in _pendingSources)
        {
            source.ClearDomainEvents();
        }

        _pendingSources.Clear();
        _pendingMessages.Clear();
    }

    private void DiscardPending(DbContext? context)
    {
        if (context is not null)
        {
            foreach (var message in _pendingMessages)
            {
                context.Entry(message).State = EntityState.Detached;
            }
        }

        _pendingSources.Clear();
        _pendingMessages.Clear();
    }
}
