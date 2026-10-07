using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Analytics;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class AnalyticsSyncStateConfiguration : IEntityTypeConfiguration<AnalyticsSyncState>
{
    public void Configure(EntityTypeBuilder<AnalyticsSyncState> builder)
    {
        builder.ToTable("AnalyticsSyncStates");
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).ValueGeneratedNever();
        builder.Property(state => state.ProviderKey).HasMaxLength(AnalyticsSyncState.ProviderKeyMaxLength).IsRequired();
        builder.Property(state => state.LastFailureCode).HasMaxLength(AnalyticsSyncState.FailureCodeMaxLength);
        builder.Property(state => state.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(state => state.IsSyncing);
        builder.HasIndex(state => state.ProviderKey).IsUnique();
    }
}
