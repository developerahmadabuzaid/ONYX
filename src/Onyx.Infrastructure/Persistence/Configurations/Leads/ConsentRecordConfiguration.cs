using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("ConsentRecords");
        builder.HasKey(consent => consent.Id);
        builder.Property(consent => consent.Id).ValueGeneratedNever();
        builder.Property(consent => consent.ConsentVersion).HasMaxLength(ConsentRecord.VersionMaxLength).IsRequired();
        builder.Property(consent => consent.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Ignore(consent => consent.HasMarketingConsent);

        builder.HasOne<Inquiry>().WithMany().HasForeignKey(consent => consent.InquiryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Lead>().WithMany().HasForeignKey(consent => consent.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(consent => consent.InquiryId).IsUnique();
        builder.HasIndex(consent => consent.LeadId);
    }
}
