using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Auditing;

namespace Onyx.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();

        builder.Property(log => log.Action).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(log => log.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(log => log.EntityType).HasMaxLength(AuditLog.EntityTypeMaxLength).IsRequired();
        builder.Property(log => log.EntityId).HasMaxLength(AuditLog.EntityIdMaxLength);
        builder.Property(log => log.Changes).HasColumnType("jsonb");
        builder.Property(log => log.UserName).HasMaxLength(AuditLog.UserNameMaxLength);
        builder.Property(log => log.IpAddress).HasMaxLength(AuditLog.IpAddressMaxLength);
        builder.Property(log => log.CorrelationId).HasMaxLength(AuditLog.CorrelationIdMaxLength);

        builder.HasIndex(log => log.OccurredAtUtc);
        builder.HasIndex(log => new { log.EntityType, log.EntityId });
        builder.HasIndex(log => log.UserId);
    }
}
