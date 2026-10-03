using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class JobDefinitionConfiguration : IEntityTypeConfiguration<JobDefinition>
{
    public void Configure(EntityTypeBuilder<JobDefinition> builder)
    {
        builder.ToTable("JobDefinitions");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Id).ValueGeneratedNever();

        builder.Property(job => job.Name).HasMaxLength(JobDefinition.NameMaxLength).IsRequired();
        builder.Property(job => job.Schedule).HasMaxLength(JobDefinition.ScheduleMaxLength).IsRequired();
        builder.Property(job => job.LastStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(job => job.LockedBy).HasMaxLength(200);

        builder.HasIndex(job => job.Name).IsUnique();
        builder.HasIndex(job => job.NextRunAtUtc);
    }
}
