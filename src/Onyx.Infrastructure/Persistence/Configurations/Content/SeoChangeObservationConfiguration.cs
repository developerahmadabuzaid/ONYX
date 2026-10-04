using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SeoChangeObservationConfiguration : IEntityTypeConfiguration<SeoChangeObservation>
{
    public void Configure(EntityTypeBuilder<SeoChangeObservation> builder)
    {
        builder.ToTable("SeoChangeObservations");
        builder.HasKey(observation => observation.Id);
        builder.Property(observation => observation.Id).ValueGeneratedNever();
        builder.Property(observation => observation.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(observation => observation.Evidence).HasJsonColumn().IsRequired();
        builder.Property(observation => observation.Assessment).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(observation => observation.Note).HasMaxLength(SeoChangeObservation.NoteMaxLength);

        builder.HasOne<SeoRecommendation>().WithMany().HasForeignKey(observation => observation.RecommendationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(observation => new { observation.RecommendationId, observation.RecordedAtUtc });
    }
}
