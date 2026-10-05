using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Booking;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class BookingSettingsConfiguration : IEntityTypeConfiguration<BookingSettings>
{
    public void Configure(EntityTypeBuilder<BookingSettings> builder)
    {
        builder.ToTable("BookingSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.Key).HasMaxLength(20).IsRequired();
        builder.Property(settings => settings.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(settings => settings.IsBookable);

        // One settings row: concurrent first saves cannot create a second one.
        builder.HasIndex(settings => settings.Key).IsUnique();
    }
}
