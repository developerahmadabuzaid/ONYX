using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Infrastructure.Email;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("EmailMessages");
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.TemplateKey).HasMaxLength(EmailMessage.TemplateKeyMaxLength).IsRequired();
        builder.Property(message => message.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(message => message.ToAddress).HasMaxLength(EmailMessage.AddressMaxLength).IsRequired();
        builder.Property(message => message.ToName).HasMaxLength(EmailMessage.NameMaxLength);
        builder.Property(message => message.Subject).HasMaxLength(EmailMessage.SubjectMaxLength).IsRequired();
        builder.Property(message => message.HtmlBody).IsRequired();
        builder.Property(message => message.TextBody).IsRequired();
        builder.Property(message => message.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.LockedBy).HasMaxLength(EmailMessage.LockedByMaxLength);
        builder.Property(message => message.LastError).HasMaxLength(EmailMessage.LastErrorMaxLength);
        builder.Property(message => message.CorrelationId).HasMaxLength(EmailMessage.CorrelationIdMaxLength);

        builder.HasIndex(message => message.CreatedAtUtc)
            .HasDatabaseName("ix_email_messages_pending")
            .HasFilter("status = 'Pending'");
    }
}
