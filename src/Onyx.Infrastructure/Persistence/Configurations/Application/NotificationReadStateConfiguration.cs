using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Notifications;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

internal sealed class NotificationReadStateConfiguration : IEntityTypeConfiguration<NotificationReadState>
{
    public void Configure(EntityTypeBuilder<NotificationReadState> builder)
    {
        builder.ToTable("NotificationReadStates");
        builder.HasKey(state => new { state.NotificationId, state.UserId });

        builder.HasOne<AdminNotification>()
            .WithMany()
            .HasForeignKey(state => state.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(state => state.UserId);
    }
}
