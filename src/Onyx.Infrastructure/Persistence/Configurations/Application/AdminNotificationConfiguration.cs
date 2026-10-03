using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Onyx.Domain.Notifications;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class AdminNotificationConfiguration : IEntityTypeConfiguration<AdminNotification>
{
    private static readonly ValueConverter<IReadOnlyDictionary<string, string>, string> ParametersConverter = new(
        parameters => JsonSerializer.Serialize(parameters, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<Dictionary<string, string>>(json, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

    private static readonly ValueComparer<IReadOnlyDictionary<string, string>> ParametersComparer = new(
        (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
        parameters => JsonSerializer.Serialize(parameters, (JsonSerializerOptions?)null).GetHashCode(StringComparison.Ordinal),
        parameters => new Dictionary<string, string>(parameters));

    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.ToTable("AdminNotifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(notification => notification.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.RequiredPermission).HasMaxLength(AdminNotification.RequiredPermissionMaxLength).IsRequired();
        builder.Property(notification => notification.ResourceType).HasMaxLength(AdminNotification.ResourceTypeMaxLength);
        builder.Property(notification => notification.ResourceId).HasMaxLength(AdminNotification.ResourceIdMaxLength);
        builder.Property(notification => notification.Parameters)
            .HasConversion(ParametersConverter, ParametersComparer)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(notification => new { notification.RequiredPermission, notification.CreatedAtUtc });
    }
}
