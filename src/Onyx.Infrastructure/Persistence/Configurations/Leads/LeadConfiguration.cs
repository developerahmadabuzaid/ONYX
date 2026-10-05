using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Leads;
using Onyx.Infrastructure.Identity;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(lead => lead.Id);
        builder.Property(lead => lead.Id).ValueGeneratedNever();
        builder.Property(lead => lead.DisplayName).HasMaxLength(ContactDetails.NameMaxLength).IsRequired();
        builder.Property(lead => lead.NormalizedEmail).HasMaxLength(ContactDetails.EmailMaxLength).IsRequired();
        builder.Property(lead => lead.Company).HasMaxLength(ContactDetails.CompanyMaxLength);
        builder.Property(lead => lead.Stage).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(lead => lead.Source).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(lead => lead.Priority).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(lead => lead.ScoreReasons).HasJsonColumn().IsRequired();
        builder.Property(lead => lead.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(lead => lead.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(lead => lead.PendingActivities);
        builder.Ignore(lead => lead.IsOpen);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(lead => lead.OwnerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(lead => new { lead.NormalizedEmail, lead.Stage });
        builder.HasIndex(lead => new { lead.Stage, lead.LastInquiryAtUtc });
        builder.HasIndex(lead => lead.OwnerId);
    }
}
