using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class CallToActionConfiguration : IEntityTypeConfiguration<CallToAction>
{
    public void Configure(EntityTypeBuilder<CallToAction> builder)
    {
        builder.ToTable("CallToActions");
        builder.HasKey(cta => cta.Id);
        builder.Property(cta => cta.Id).ValueGeneratedNever();

        builder.Property(cta => cta.Key).HasMaxLength(CallToAction.KeyMaxLength).IsRequired();
        builder.Property(cta => cta.ActionType).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(cta => cta.TargetUrl).HasMaxLength(ContentBodyRules.MaxUrlLength);
        builder.Property(cta => cta.Texts).HasJsonColumn().IsRequired();
        builder.Property(cta => cta.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(cta => cta.Key).IsUnique();
    }
}
