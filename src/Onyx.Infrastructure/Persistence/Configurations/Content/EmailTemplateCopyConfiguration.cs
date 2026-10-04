using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class EmailTemplateCopyConfiguration : IEntityTypeConfiguration<EmailTemplateCopy>
{
    public void Configure(EntityTypeBuilder<EmailTemplateCopy> builder)
    {
        builder.ToTable("EmailTemplateCopies");
        builder.HasKey(copy => copy.Id);
        builder.Property(copy => copy.Id).ValueGeneratedNever();
        builder.Property(copy => copy.TemplateKey).HasMaxLength(EmailTemplateCopy.KeyMaxLength).IsRequired();
        builder.Property(copy => copy.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(copy => copy.Subject).HasMaxLength(EmailTemplateCopy.SubjectMaxLength).IsRequired();
        builder.Property(copy => copy.Heading).HasMaxLength(EmailTemplateCopy.HeadingMaxLength).IsRequired();
        builder.Property(copy => copy.Paragraphs).HasJsonColumn().IsRequired();
        builder.Property(copy => copy.ActionLabel).HasMaxLength(EmailTemplateCopy.ActionLabelMaxLength);
        builder.Property(copy => copy.Footer).HasMaxLength(EmailTemplateCopy.FooterMaxLength);
        builder.Property(copy => copy.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(copy => new { copy.TemplateKey, copy.LanguageCode }).IsUnique();
    }
}
