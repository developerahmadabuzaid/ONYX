using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Onyx.Domain.Common;
using Onyx.Infrastructure.Persistence.Configurations;
using Onyx.Infrastructure.Persistence.Conventions;

namespace Onyx.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    /// <summary>Call from <c>ConfigureConventions</c> in every Onyx DbContext.</summary>
    public static ModelConfigurationBuilder ApplyOnyxConventionDefaults(this ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Domain events live in memory until the outbox interceptor persists them; they are never mapped columns.
        configurationBuilder.IgnoreAny<IReadOnlyCollection<IDomainEvent>>();
        return configurationBuilder;
    }

    /// <summary>Audit log and outbox tables shared by every Onyx DbContext (the application context and test-only contexts).</summary>
    public static ModelBuilder ApplyCoreInfrastructureModel(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        return modelBuilder;
    }

    /// <summary>Must run last in OnModelCreating: soft-delete filters, then snake_case names for every table, column, key, FK and index.</summary>
    public static ModelBuilder ApplyOnyxConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ApplySoftDeleteFilters(modelBuilder);
        ApplySnakeCaseNames(modelBuilder);
        return modelBuilder;
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType) || entityType.BaseType is not null)
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    private static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null && !entityType.IsOwned())
            {
                entityType.SetTableName(SnakeCaseNaming.ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(SnakeCaseNaming.ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entityType.GetKeys())
            {
                key.SetName(SnakeCaseNaming.ToSnakeCase(key.GetName()!));
            }

            foreach (var foreignKey in entityType.GetForeignKeys())
            {
                foreignKey.SetConstraintName(SnakeCaseNaming.ToSnakeCase(foreignKey.GetConstraintName()!));
            }

            foreach (var index in entityType.GetIndexes())
            {
                index.SetDatabaseName(SnakeCaseNaming.ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }
}
