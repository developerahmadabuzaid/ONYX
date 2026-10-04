using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentClusterMemberConfiguration : IEntityTypeConfiguration<ContentClusterMember>
{
    public void Configure(EntityTypeBuilder<ContentClusterMember> builder)
    {
        builder.ToTable("ContentClusterMembers");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(member => member.ContentItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(member => new { member.ClusterId, member.ContentItemId }).IsUnique();
        builder.HasIndex(member => member.ContentItemId);
    }
}
