using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class LeadActivityConfiguration : IEntityTypeConfiguration<LeadActivity>
{
    public void Configure(EntityTypeBuilder<LeadActivity> builder)
    {
        builder.ToTable("LeadActivities");
        builder.HasKey(activity => activity.Id);
        builder.Property(activity => activity.Id).ValueGeneratedNever();
        builder.Property(activity => activity.Kind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(activity => activity.Detail).HasMaxLength(LeadActivity.DetailMaxLength);
        builder.HasOne<Lead>().WithMany().HasForeignKey(activity => activity.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(activity => new { activity.LeadId, activity.OccurredAtUtc });
    }
}
