using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Onyx.Application.Abstractions;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Writes an <see cref="AuditLog"/> row, in the same transaction, for every change to an <see cref="IAuditable"/> entity.
/// <see cref="SensitiveDataAttribute"/> values are redacted; <see cref="NotAuditedAttribute"/> properties are omitted.
/// Must run after <see cref="AuditFieldsInterceptor"/> so soft deletes are recognised.
/// </summary>
public sealed class AuditLogInterceptor(ICurrentUser currentUser, IRequestContext requestContext, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Stamps and soft-delete bookkeeping are visible on the row itself; repeating them in every change set is noise.
    private static readonly HashSet<string> BookkeepingProperties =
    [
        nameof(IHasAuditFields.CreatedAtUtc),
        nameof(IHasAuditFields.CreatedById),
        nameof(IHasAuditFields.UpdatedAtUtc),
        nameof(IHasAuditFields.UpdatedById),
        nameof(ISoftDeletable.DeletedAtUtc),
        nameof(ISoftDeletable.DeletedById),
    ];

    private readonly List<AuditLog> _pending = [];

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
        _pending.Clear();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
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
        var entries = context.ChangeTracker.Entries()
            .Where(entry => entry.Entity is IAuditable && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var action = ResolveAction(entry);
            var changes = BuildChanges(entry);
            if (action == AuditAction.Updated && changes.Count == 0)
            {
                continue;
            }

            var log = AuditLog.Record(
                now,
                action,
                ((IAuditable)entry.Entity).AuditCategory,
                entry.Metadata.ClrType.Name,
                (entry.Entity as Entity)?.Id.ToString(),
                JsonSerializer.Serialize(changes, SerializerOptions),
                currentUser.UserId,
                currentUser.UserName,
                requestContext.IpAddress,
                requestContext.CorrelationId);

            context.Set<AuditLog>().Add(log);
            _pending.Add(log);
        }
    }

    private void DiscardPending(DbContext? context)
    {
        if (context is not null)
        {
            foreach (var log in _pending)
            {
                context.Entry(log).State = EntityState.Detached;
            }
        }

        _pending.Clear();
    }

    private static AuditAction ResolveAction(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => AuditAction.Created,
        EntityState.Deleted => AuditAction.Deleted,
        _ when entry.Entity is ISoftDeletable && IsBecomingDeleted(entry) => AuditAction.SoftDeleted,
        _ => AuditAction.Updated,
    };

    private static bool IsBecomingDeleted(EntityEntry entry)
    {
        var property = entry.Property(nameof(ISoftDeletable.IsDeleted));
        return property.CurrentValue is true && property.OriginalValue is false;
    }

    private static Dictionary<string, object?> BuildChanges(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            var propertyInfo = property.Metadata.PropertyInfo;
            if (BookkeepingProperties.Contains(name) || property.Metadata.IsConcurrencyToken || IsMarked<NotAuditedAttribute>(propertyInfo))
            {
                continue;
            }

            var sensitive = IsMarked<SensitiveDataAttribute>(propertyInfo);
            object? Display(object? value) => sensitive ? RedactedValue : value;

            switch (entry.State)
            {
                case EntityState.Added:
                    changes[name] = new { New = Display(property.CurrentValue) };
                    break;
                case EntityState.Deleted:
                    changes[name] = new { Old = Display(property.OriginalValue) };
                    break;
                case EntityState.Modified when property.IsModified && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new { Old = Display(property.OriginalValue), New = Display(property.CurrentValue) };
                    break;
            }
        }

        return changes;
    }

    private static bool IsMarked<TAttribute>(PropertyInfo? propertyInfo)
        where TAttribute : Attribute =>
        propertyInfo?.GetCustomAttribute<TAttribute>(inherit: true) is not null;
}
