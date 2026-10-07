using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Content;
using Onyx.Domain.News;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>
/// Category names. The seeded English names are the launch categories of architecture.md §17.3; the seeded Arabic names are
/// working translations for native-copy review before launch (editable in Admin → News categories).
/// </summary>
internal sealed class NewsCategoryTranslationConfiguration : IEntityTypeConfiguration<NewsCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<NewsCategoryTranslation> builder)
    {
        builder.ToTable("NewsCategoryTranslations");
        builder.HasKey(translation => translation.Id);
        builder.Property(translation => translation.Id).ValueGeneratedNever();
        builder.Property(translation => translation.LanguageCode).HasMaxLength(10).IsRequired();
        builder.Property(translation => translation.Name).HasMaxLength(NewsCategoryTranslation.NameMaxLength).IsRequired();
        builder.Property(translation => translation.Slug).HasMaxLength(Slug.MaxLength).IsRequired();
        builder.HasIndex(translation => new { translation.NewsCategoryId, translation.LanguageCode }).IsUnique();
        builder.HasIndex(translation => new { translation.LanguageCode, translation.Slug }).IsUnique();

        builder.HasData(
            Seed(1, NewsCategoryConfiguration.ArtificialIntelligenceId, "ar", "الذكاء الاصطناعي", "الذكاء-الاصطناعي"),
            Seed(2, NewsCategoryConfiguration.ArtificialIntelligenceId, "en", "Artificial Intelligence", "artificial-intelligence"),
            Seed(3, NewsCategoryConfiguration.SoftwareTechnologyId, "ar", "البرمجيات والتقنية", "البرمجيات-والتقنية"),
            Seed(4, NewsCategoryConfiguration.SoftwareTechnologyId, "en", "Software & Technology", "software-technology"),
            Seed(5, NewsCategoryConfiguration.DigitalMarketingId, "ar", "التسويق الرقمي", "التسويق-الرقمي"),
            Seed(6, NewsCategoryConfiguration.DigitalMarketingId, "en", "Digital Marketing", "digital-marketing"),
            Seed(7, NewsCategoryConfiguration.CybersecurityId, "ar", "الأمن السيبراني", "الأمن-السيبراني"),
            Seed(8, NewsCategoryConfiguration.CybersecurityId, "en", "Cybersecurity", "cybersecurity"));
    }

    private static object Seed(int number, Guid categoryId, string language, string name, string slug) =>
        new { Id = new Guid($"0192f0c0-0001-7000-8000-{number:D12}"), NewsCategoryId = categoryId, LanguageCode = language, Name = name, Slug = slug };
}
