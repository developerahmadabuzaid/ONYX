using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Privacy;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class CookieConsentReceiptConfiguration : IEntityTypeConfiguration<CookieConsentReceipt>
{
    public void Configure(EntityTypeBuilder<CookieConsentReceipt> builder)
    {
        builder.ToTable("CookieConsentReceipts");
        builder.HasKey(receipt => receipt.Id);
        builder.Property(receipt => receipt.Id).ValueGeneratedNever();
        builder.Property(receipt => receipt.PolicyVersion).HasMaxLength(CookieConsentReceipt.PolicyVersionMaxLength).IsRequired();
        builder.Property(receipt => receipt.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(receipt => receipt.IpHash).HasMaxLength(CookieConsentReceipt.IpHashLength);

        builder.HasIndex(receipt => receipt.ConsentId);
        builder.HasIndex(receipt => receipt.RecordedAtUtc);
    }
}
