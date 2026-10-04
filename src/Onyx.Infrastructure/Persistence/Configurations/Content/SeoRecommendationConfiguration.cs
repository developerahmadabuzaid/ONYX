using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class SeoRecommendationConfiguration : IEntityTypeConfiguration<SeoRecommendation>
{
    public void Configure(EntityTypeBuilder<SeoRecommendation> builder)
    {
        builder.ToTable("SeoRecommendations");
        builder.HasKey(recommendation => recommendation.Id);
        builder.Property(recommendation => recommendation.Id).ValueGeneratedNever();
        builder.Property(recommendation => recommendation.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength);
        builder.Property(recommendation => recommendation.Kind).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(recommendation => recommendation.Origin).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(recommendation => recommendation.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(recommendation => recommendation.Basis).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(recommendation => recommendation.Title).HasMaxLength(SeoRecommendation.TitleMaxLength).IsRequired();
        builder.Property(recommendation => recommendation.DetectedIssue).HasMaxLength(SeoRecommendation.IssueMaxLength).IsRequired();
        builder.Property(recommendation => recommendation.Reasoning).HasMaxLength(SeoRecommendation.ReasoningMaxLength);
        builder.Property(recommendation => recommendation.ProposedChanges).HasJsonColumn().IsRequired();
        builder.Property(recommendation => recommendation.Evidence).HasJsonColumn().IsRequired();
        builder.Property(recommendation => recommendation.DecisionNote).HasMaxLength(SeoRecommendation.DecisionNoteMaxLength);
        builder.Property(recommendation => recommendation.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(recommendation => recommendation.ContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<ContentCluster>().WithMany().HasForeignKey(recommendation => recommendation.ClusterId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(recommendation => new { recommendation.Status, recommendation.CreatedAtUtc });
        builder.HasIndex(recommendation => new { recommendation.ContentItemId, recommendation.Kind, recommendation.LanguageCode });
    }
}
