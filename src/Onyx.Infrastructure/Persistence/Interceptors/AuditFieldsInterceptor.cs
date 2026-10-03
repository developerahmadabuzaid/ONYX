using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Onyx.Application.Abstractions;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Persistence.Interceptors;

/// <summary>Stamps creation/modification fields and turns deletes of <see cref="ISoftDeletable"/> entities into soft deletes. Registered first.</summary>
public sealed class AuditFieldsInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable })
            {
                entry.State = EntityState.Modified;
                Set(entry, nameof(ISoftDeletable.IsDeleted), true);
                Set(entry, nameof(ISoftDeletable.DeletedAtUtc), now);
                Set(entry, nameof(ISoftDeletable.DeletedById), userId);
            }

            if (entry.Entity is not IHasAuditFields)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                Set(entry, nameof(IHasAuditFields.CreatedAtUtc), now);
                Set(entry, nameof(IHasAuditFields.CreatedById), userId);
            }
            else if (entry.State == EntityState.Modified)
            {
                Set(entry, nameof(IHasAuditFields.UpdatedAtUtc), now);
                Set(entry, nameof(IHasAuditFields.UpdatedById), userId);
            }
        }
    }

    private static void Set(EntityEntry entry, string propertyName, object? value) =>
        entry.Property(propertyName).CurrentValue = value;
}
