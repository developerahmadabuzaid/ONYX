using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class CompetitorReferenceConfiguration : IEntityTypeConfiguration<CompetitorReference>
{
    public void Configure(EntityTypeBuilder<CompetitorReference> builder)
    {
        builder.ToTable("CompetitorReferences");
        builder.HasKey(reference => reference.Id);
        builder.Property(reference => reference.Id).ValueGeneratedNever();
        builder.Property(reference => reference.Name).HasMaxLength(CompetitorReference.NameMaxLength).IsRequired();
        builder.Property(reference => reference.Domain).HasMaxLength(CompetitorReference.DomainMaxLength).IsRequired();
        builder.Property(reference => reference.Markets).HasJsonColumn().IsRequired();
        builder.Property(reference => reference.Languages).HasJsonColumn().IsRequired();
        builder.Property(reference => reference.Topics).HasJsonColumn().IsRequired();
        builder.Property(reference => reference.Notes).HasMaxLength(CompetitorReference.NotesMaxLength);
        builder.Property(reference => reference.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(reference => reference.Domain).IsUnique();
    }
}
