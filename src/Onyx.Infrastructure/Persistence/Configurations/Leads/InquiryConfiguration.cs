using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class InquiryConfiguration : IEntityTypeConfiguration<Inquiry>
{
    public void Configure(EntityTypeBuilder<Inquiry> builder)
    {
        builder.ToTable("Inquiries");
        builder.HasKey(inquiry => inquiry.Id);
        builder.Property(inquiry => inquiry.Id).ValueGeneratedNever();
        builder.Property(inquiry => inquiry.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(inquiry => inquiry.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(inquiry => inquiry.Name).HasMaxLength(ContactDetails.NameMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.Email).HasMaxLength(ContactDetails.EmailMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.NormalizedEmail).HasMaxLength(ContactDetails.EmailMaxLength).IsRequired();
        builder.Property(inquiry => inquiry.Phone).HasMaxLength(ContactDetails.PhoneMaxLength);
        builder.Property(inquiry => inquiry.Company).HasMaxLength(ContactDetails.CompanyMaxLength);
        builder.Property(inquiry => inquiry.Country).HasMaxLength(2);
        builder.Property(inquiry => inquiry.Message).HasMaxLength(Inquiry.MessageMaxLength);
        builder.Property(inquiry => inquiry.Answers).HasJsonColumn().IsRequired();
        builder.Property(inquiry => inquiry.Attribution).HasJsonColumn().IsRequired();
        builder.Ignore(inquiry => inquiry.IsErased);

        builder.HasOne<Lead>().WithMany().HasForeignKey(inquiry => inquiry.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(inquiry => new { inquiry.LeadId, inquiry.SubmittedAtUtc });
        builder.HasIndex(inquiry => inquiry.SubmittedAtUtc);
        builder.HasIndex(inquiry => inquiry.NormalizedEmail);
    }
}
