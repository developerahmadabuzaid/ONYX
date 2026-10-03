using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Persistence.Outbox;

namespace Onyx.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Type).HasMaxLength(OutboxMessage.TypeMaxLength).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(message => message.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);
        builder.Property(message => message.LockedBy).HasMaxLength(OutboxMessage.LockedByMaxLength);

        // Partial index: the dispatcher only scans deliverable messages, oldest first.
        builder.HasIndex(message => message.OccurredAtUtc)
            .HasDatabaseName("ix_outbox_messages_unprocessed")
            .HasFilter("processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");
    }
}
