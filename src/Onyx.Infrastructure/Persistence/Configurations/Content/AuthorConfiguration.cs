using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Insights;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class AuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        builder.ToTable("Authors");
        builder.HasKey(author => author.Id);
        builder.Property(author => author.Id).ValueGeneratedNever();
        builder.Property(author => author.Names).HasJsonColumn().IsRequired();
        builder.Property(author => author.ConsentNote).HasMaxLength(Author.ConsentMaxLength).IsRequired();
        builder.Property(author => author.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
    }
}
