using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Privacy;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class RetentionPolicyConfiguration : IEntityTypeConfiguration<RetentionPolicy>
{
    public void Configure(EntityTypeBuilder<RetentionPolicy> builder)
    {
        builder.ToTable("RetentionPolicies", table => table.HasCheckConstraint(
            "ck_retention_policies_period", $"period_days IS NULL OR (period_days BETWEEN 1 AND {RetentionPolicy.MaxPeriodDays})"));
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).ValueGeneratedNever();
        builder.Property(policy => policy.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(policy => policy.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(policy => policy.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(policy => policy.IsActive);

        // One policy per category, even if two administrators save the first one at the same moment.
        builder.HasIndex(policy => policy.Category).IsUnique();
    }
}
