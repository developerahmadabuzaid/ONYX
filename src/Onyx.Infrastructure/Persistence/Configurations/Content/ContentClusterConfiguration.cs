using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class ContentClusterConfiguration : IEntityTypeConfiguration<ContentCluster>
{
    public void Configure(EntityTypeBuilder<ContentCluster> builder)
    {
        builder.ToTable("ContentClusters");
        builder.HasKey(cluster => cluster.Id);
        builder.Property(cluster => cluster.Id).ValueGeneratedNever();
        builder.Ignore(cluster => cluster.PillarContentItemId);
        builder.Property(cluster => cluster.Name).HasMaxLength(ContentCluster.NameMaxLength).IsRequired();
        builder.Property(cluster => cluster.Description).HasMaxLength(ContentCluster.DescriptionMaxLength);
        builder.Property(cluster => cluster.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");

        builder.HasMany(cluster => cluster.Members).WithOne().HasForeignKey(member => member.ClusterId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(cluster => cluster.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
