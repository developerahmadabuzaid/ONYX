using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Booking;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class AvailabilityExceptionConfiguration : IEntityTypeConfiguration<AvailabilityException>
{
    public void Configure(EntityTypeBuilder<AvailabilityException> builder)
    {
        builder.ToTable("AvailabilityExceptions");
        builder.HasKey(exception => exception.Id);
        builder.Property(exception => exception.Id).ValueGeneratedNever();
        builder.Property(exception => exception.Reason).HasMaxLength(AvailabilityException.ReasonMaxLength);
        builder.HasIndex(exception => exception.Date).IsUnique();
    }
}
