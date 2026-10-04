using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentPerformanceSnapshotConfiguration : IEntityTypeConfiguration<ContentPerformanceSnapshot>
{
    public void Configure(EntityTypeBuilder<ContentPerformanceSnapshot> builder)
    {
        builder.ToTable("ContentPerformanceSnapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
        builder.Property(snapshot => snapshot.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(snapshot => snapshot.Source).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(snapshot => snapshot.Metrics).HasJsonColumn().IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(snapshot => snapshot.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(snapshot => new { snapshot.ContentItemId, snapshot.LanguageCode, snapshot.PeriodEndUtc });
    }
}
