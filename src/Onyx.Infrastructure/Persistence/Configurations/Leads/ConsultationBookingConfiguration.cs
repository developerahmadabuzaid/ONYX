using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Booking;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ConsultationBookingConfiguration : IEntityTypeConfiguration<ConsultationBooking>
{
    public const string ConfirmedSlotIndex = "ix_consultation_bookings_confirmed_slot";

    public void Configure(EntityTypeBuilder<ConsultationBooking> builder)
    {
        builder.ToTable("ConsultationBookings", table => table.HasCheckConstraint("ck_consultation_bookings_range", "end_utc > start_utc"));
        builder.HasKey(booking => booking.Id);
        builder.Property(booking => booking.Id).ValueGeneratedNever();
        builder.Property(booking => booking.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(booking => booking.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(booking => booking.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(booking => booking.ManageTokenHash).HasMaxLength(ConsultationBooking.HashLength).IsFixedLength();
        builder.Property(booking => booking.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(booking => booking.IsActive);

        builder.HasOne<Lead>().WithMany().HasForeignKey(booking => booking.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Inquiry>().WithMany().HasForeignKey(booking => booking.InquiryId).OnDelete(DeleteBehavior.Restrict);

        // The final guard against double booking: one confirmed booking per start time, even under concurrent requests.
        builder.HasIndex(booking => booking.StartUtc).IsUnique().HasFilter("status = 'Confirmed'").HasDatabaseName(ConfirmedSlotIndex);

        // Not unique: on reschedule the hash moves to the replacement in the same save, and 256-bit random tokens cannot collide.
        builder.HasIndex(booking => booking.ManageTokenHash).HasFilter("manage_token_hash IS NOT NULL");
        builder.HasIndex(booking => booking.LeadId);
        builder.HasIndex(booking => new { booking.Status, booking.StartUtc });
    }
}
