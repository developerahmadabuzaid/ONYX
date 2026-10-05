using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Leads;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class LeadNoteConfiguration : IEntityTypeConfiguration<LeadNote>
{
    public void Configure(EntityTypeBuilder<LeadNote> builder)
    {
        builder.ToTable("LeadNotes");
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Id).ValueGeneratedNever();
        builder.Property(note => note.Body).HasMaxLength(LeadNote.BodyMaxLength).IsRequired();
        builder.HasOne<Lead>().WithMany().HasForeignKey(note => note.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(note => new { note.LeadId, note.CreatedAtUtc });
    }
}
