using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Brand;
using Onyx.Domain.Content;
using Onyx.Domain.Intelligence;
using Onyx.Domain.Knowledge;
using Onyx.Domain.Media;
using Onyx.Domain.Messaging;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class InformationRequestConfiguration : IEntityTypeConfiguration<InformationRequest>
{
    public void Configure(EntityTypeBuilder<InformationRequest> builder)
    {
        builder.ToTable("InformationRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Question).HasMaxLength(InformationRequest.QuestionMaxLength).IsRequired();
        builder.Property(request => request.Context).HasMaxLength(InformationRequest.ContextMaxLength);
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.ResolutionNote).HasMaxLength(InformationRequest.ResolutionMaxLength);

        builder.HasOne<ContentItem>().WithMany().HasForeignKey(request => request.ContentItemId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<KnowledgeEntry>().WithMany().HasForeignKey(request => request.KnowledgeEntryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(request => request.Status);
    }
}
