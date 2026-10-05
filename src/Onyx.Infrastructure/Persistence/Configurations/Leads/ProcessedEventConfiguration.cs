using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");
        builder.HasKey(processed => processed.Id);
        builder.Property(processed => processed.Id).ValueGeneratedNever();
        builder.Property(processed => processed.Key).HasMaxLength(ProcessedEvent.KeyMaxLength).IsRequired();
        builder.HasIndex(processed => processed.Key).IsUnique();
    }
}
