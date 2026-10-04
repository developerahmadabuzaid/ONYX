using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentPreviewTokenConfiguration : IEntityTypeConfiguration<ContentPreviewToken>
{
    public void Configure(EntityTypeBuilder<ContentPreviewToken> builder)
    {
        builder.ToTable("ContentPreviewTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.LanguageCode).HasMaxLength(ContentTranslation.LanguageCodeMaxLength).IsRequired();
        builder.Property(token => token.TokenHash).HasMaxLength(ContentPreviewToken.HashLength).IsFixedLength().IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(token => token.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.ExpiresAtUtc);
    }
}
