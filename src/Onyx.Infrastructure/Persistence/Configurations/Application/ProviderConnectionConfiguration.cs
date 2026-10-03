using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Providers;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ProviderConnectionConfiguration : IEntityTypeConfiguration<ProviderConnection>
{
    public void Configure(EntityTypeBuilder<ProviderConnection> builder)
    {
        builder.ToTable("ProviderConnections");
        builder.HasKey(connection => connection.Id);
        builder.Property(connection => connection.Id).ValueGeneratedNever();

        builder.Property(connection => connection.ProviderKey).HasMaxLength(ProviderConnection.ProviderKeyMaxLength).IsRequired();
        builder.Property(connection => connection.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(connection => connection.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(connection => connection.LastError).HasMaxLength(ProviderConnection.LastErrorMaxLength);

        // PostgreSQL's xmin system column: optimistic concurrency without an extra column. The name is explicit so the
        // snake_case convention cannot turn it into a real "version" column.
        builder.Property(connection => connection.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(connection => connection.ProviderKey).IsUnique();
    }
}
