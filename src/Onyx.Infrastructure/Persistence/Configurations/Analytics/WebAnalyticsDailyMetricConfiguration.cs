using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Analytics;
using Onyx.Domain.Content;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>Daily web-analytics aggregates; the unique key (provider, date, kind, dimension hash) makes re-syncs idempotent.</summary>
internal sealed class WebAnalyticsDailyMetricConfiguration : IEntityTypeConfiguration<WebAnalyticsDailyMetric>
{
    public void Configure(EntityTypeBuilder<WebAnalyticsDailyMetric> builder)
    {
        builder.ToTable("WebAnalyticsDailyMetrics");
        builder.HasKey(metric => metric.Id);
        builder.Property(metric => metric.Id).ValueGeneratedNever();
        builder.Property(metric => metric.ProviderKey).HasMaxLength(AnalyticsSyncState.ProviderKeyMaxLength).IsRequired();
        builder.Property(metric => metric.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(metric => metric.LandingPage).HasMaxLength(AnalyticsDimensions.PathMaxLength).IsRequired();
        builder.Property(metric => metric.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(metric => metric.Source).HasMaxLength(AnalyticsDimensions.ShortMaxLength).IsRequired();
        builder.Property(metric => metric.Medium).HasMaxLength(AnalyticsDimensions.ShortMaxLength).IsRequired();
        builder.Property(metric => metric.Campaign).HasMaxLength(AnalyticsDimensions.CampaignMaxLength).IsRequired();
        builder.Property(metric => metric.Country).HasMaxLength(AnalyticsDimensions.ShortMaxLength).IsRequired();
        builder.Property(metric => metric.DeviceCategory).HasMaxLength(WebAnalyticsDailyMetric.DeviceMaxLength).IsRequired();
        builder.Property(metric => metric.EventName).HasMaxLength(WebAnalyticsDailyMetric.EventNameMaxLength).IsRequired();
        builder.Property(metric => metric.DimensionHash).HasMaxLength(AnalyticsDimensions.HashLength).IsFixedLength().IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(metric => metric.ContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(metric => new { metric.ProviderKey, metric.Date, metric.Kind, metric.DimensionHash }).IsUnique();
        builder.HasIndex(metric => new { metric.ProviderKey, metric.Kind, metric.Date });
        builder.HasIndex(metric => metric.ContentItemId).HasFilter("content_item_id IS NOT NULL");
    }
}
