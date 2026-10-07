using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>
/// Categories, seeded with the four launch categories of architecture.md §17.3 (CLAUDE.md §19: news categories are seeded).
/// Seeds carry no keywords: classification falls back to each source's default category until editors add keywords.
/// </summary>
internal sealed class NewsCategoryConfiguration : IEntityTypeConfiguration<NewsCategory>
{
    public static readonly Guid ArtificialIntelligenceId = new("0192f0c0-0000-7000-8000-000000000001");
    public static readonly Guid SoftwareTechnologyId = new("0192f0c0-0000-7000-8000-000000000002");
    public static readonly Guid DigitalMarketingId = new("0192f0c0-0000-7000-8000-000000000003");
    public static readonly Guid CybersecurityId = new("0192f0c0-0000-7000-8000-000000000004");

    private static readonly DateTimeOffset SeededAt = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<NewsCategory> builder)
    {
        builder.ToTable("NewsCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Key).HasMaxLength(NewsCategory.KeyMaxLength).IsRequired();
        builder.Property(category => category.Keywords).HasJsonColumn().IsRequired();
        builder.Property(category => category.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.HasIndex(category => category.Key).IsUnique();

        builder.HasMany(category => category.Translations).WithOne().HasForeignKey(translation => translation.NewsCategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(category => category.Translations).HasField("_translations").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasData(
            Seed(ArtificialIntelligenceId, "artificial-intelligence", 1),
            Seed(SoftwareTechnologyId, "software-technology", 2),
            Seed(DigitalMarketingId, "digital-marketing", 3),
            Seed(CybersecurityId, "cybersecurity", 4));
    }

    private static object Seed(Guid id, string key, int sortOrder) =>
        new { Id = id, Key = key, SortOrder = sortOrder, Keywords = new List<string>(), CreatedAtUtc = SeededAt };
}
