using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Privacy;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.ToTable("DataSubjectRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.SubjectEmail).HasMaxLength(254);
        builder.Property(request => request.SubjectEmailHash).HasMaxLength(DataSubjectRequest.HashLength).IsFixedLength().IsRequired();
        builder.Property(request => request.RequestNote).HasMaxLength(DataSubjectRequest.NoteMaxLength);
        builder.Property(request => request.VerificationNote).HasMaxLength(DataSubjectRequest.NoteMaxLength);
        builder.Property(request => request.RejectionReason).HasMaxLength(DataSubjectRequest.NoteMaxLength);
        builder.Property(request => request.ResultSummary).HasMaxLength(DataSubjectRequest.SummaryMaxLength);
        builder.Property(request => request.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(request => request.IsOpen);

        builder.HasIndex(request => request.SubjectEmailHash);
        builder.HasIndex(request => new { request.Status, request.ReceivedAtUtc });
    }
}
