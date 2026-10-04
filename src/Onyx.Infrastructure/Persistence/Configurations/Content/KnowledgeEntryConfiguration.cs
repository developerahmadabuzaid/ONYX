using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class KnowledgeEntryConfiguration : IEntityTypeConfiguration<KnowledgeEntry>
{
    public void Configure(EntityTypeBuilder<KnowledgeEntry> builder)
    {
        builder.ToTable("KnowledgeEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Category).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.Title).HasMaxLength(KnowledgeEntry.TitleMaxLength).IsRequired();
        builder.Property(entry => entry.Statements).HasJsonColumn().IsRequired();
        builder.Property(entry => entry.SourceNote).HasMaxLength(KnowledgeEntry.SourceMaxLength);
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasIndex(entry => new { entry.Status, entry.Category });
    }
}
