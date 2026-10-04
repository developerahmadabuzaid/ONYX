using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class CtaDefaultConfiguration : IEntityTypeConfiguration<CtaDefault>
{
    public void Configure(EntityTypeBuilder<CtaDefault> builder)
    {
        builder.ToTable("CtaDefaults");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.ContentType).HasConversion<string>().HasMaxLength(30);

        builder.HasOne<CallToAction>().WithMany().HasForeignKey(entry => entry.CallToActionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(entry => entry.ContentType).IsUnique().AreNullsDistinct(false);
    }
}
