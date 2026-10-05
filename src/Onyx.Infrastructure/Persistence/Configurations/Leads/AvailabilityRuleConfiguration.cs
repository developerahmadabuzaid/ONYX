using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Booking;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("AvailabilityRules", table => table.HasCheckConstraint("ck_availability_rules_window", "end_time > start_time"));
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.DayOfWeek).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.HasIndex(rule => new { rule.DayOfWeek, rule.StartTime });
    }
}
