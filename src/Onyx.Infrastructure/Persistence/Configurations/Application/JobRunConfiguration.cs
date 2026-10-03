using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.BackgroundJobs;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("JobRuns");
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Id).ValueGeneratedNever();

        builder.Property(run => run.JobName).HasMaxLength(JobDefinition.NameMaxLength).IsRequired();
        builder.Property(run => run.WorkerId).HasMaxLength(200).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(run => run.Message).HasMaxLength(JobRun.MessageMaxLength);

        builder.HasIndex(run => new { run.JobName, run.StartedAtUtc });
    }
}
