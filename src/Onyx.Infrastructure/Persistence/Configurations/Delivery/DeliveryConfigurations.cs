using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Onyx.Domain.Delivery;
using Onyx.Infrastructure.Identity;

namespace Onyx.Infrastructure.Persistence.Configurations.Application;

/// <summary>
/// Delivery workspace tables. Projects are archived, never deleted (issues restrict deletion); child rows of a project or issue
/// (workflow, members, comments, history, links, watchers) cascade with their owner. Users live in Identity: foreign keys point at
/// the users table and restrict deletion, so history keeps its authors.
/// </summary>
internal sealed class DeliveryProjectConfiguration : IEntityTypeConfiguration<DeliveryProject>
{
    public void Configure(EntityTypeBuilder<DeliveryProject> builder)
    {
        builder.ToTable("DeliveryProjects");
        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();
        builder.Property(project => project.Key).HasMaxLength(DeliveryProject.KeyMaxLength).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(DeliveryProject.NameMaxLength).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(DeliveryProject.DescriptionMaxLength);
        builder.Property(project => project.ClientName).HasMaxLength(DeliveryProject.ClientNameMaxLength);
        builder.Property(project => project.RepositoryUrl).HasMaxLength(DeliveryText.UrlMaxLength);
        builder.Property(project => project.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(project => project.Classification).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(project => project.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(project => project.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(project => project.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(project => project.InitialStatus);

        builder.HasMany(project => project.Statuses).WithOne().HasForeignKey(status => status.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(project => project.Transitions).WithOne().HasForeignKey(transition => transition.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(project => project.Members).WithOne().HasForeignKey(member => member.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(project => project.Statuses).HasField("_statuses").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(project => project.Transitions).HasField("_transitions").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(project => project.Members).HasField("_members").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(project => project.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(project => project.Key).IsUnique();
        builder.HasIndex(project => project.Status);
    }
}

internal sealed class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("DeliveryWorkflowStatuses");
        builder.HasKey(status => status.Id);
        builder.Property(status => status.Id).ValueGeneratedNever();
        builder.Property(status => status.Name).HasMaxLength(WorkflowStatus.NameMaxLength).IsRequired();
        builder.Property(status => status.SystemKey).HasMaxLength(WorkflowStatus.SystemKeyMaxLength);
        builder.Property(status => status.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(status => new { status.ProjectId, status.Order }).IsUnique();
    }
}

internal sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("DeliveryWorkflowTransitions");
        builder.HasKey(transition => transition.Id);
        builder.Property(transition => transition.Id).ValueGeneratedNever();
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(transition => transition.FromStatusId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(transition => transition.ToStatusId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(transition => new { transition.ProjectId, transition.FromStatusId, transition.ToStatusId }).IsUnique();
    }
}

internal sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("DeliveryProjectMembers");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).ValueGeneratedNever();
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(member => new { member.ProjectId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
    }
}

internal sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> builder)
    {
        builder.ToTable("DeliveryIssues");
        builder.HasKey(issue => issue.Id);
        builder.Property(issue => issue.Id).ValueGeneratedNever();
        builder.Property(issue => issue.Key).HasMaxLength(Issue.KeyMaxLength).IsRequired();
        builder.Property(issue => issue.Title).HasMaxLength(Issue.TitleMaxLength).IsRequired();
        builder.Property(issue => issue.Description).HasMaxLength(Issue.DescriptionMaxLength);
        builder.Property(issue => issue.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(issue => issue.Priority).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(issue => issue.EstimateHours).HasPrecision(7, 2);
        builder.Property(issue => issue.Labels).HasColumnType("text[]").IsRequired();
        builder.Property(issue => issue.Components).HasColumnType("text[]").IsRequired();
        builder.Property(issue => issue.RepositoryUrl).HasMaxLength(DeliveryText.UrlMaxLength);
        builder.Property(issue => issue.PullRequestUrl).HasMaxLength(DeliveryText.UrlMaxLength);
        builder.Property(issue => issue.CommitUrl).HasMaxLength(DeliveryText.UrlMaxLength);
        builder.Property(issue => issue.BranchName).HasMaxLength(Issue.BranchMaxLength);
        builder.Property(issue => issue.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(issue => issue.IsEpic);
        builder.Ignore(issue => issue.IsSubtask);

        builder.HasOne<DeliveryProject>().WithMany().HasForeignKey(issue => issue.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkflowStatus>().WithMany().HasForeignKey(issue => issue.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(issue => issue.ReporterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(issue => issue.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Issue>().WithMany().HasForeignKey(issue => issue.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Issue>().WithMany().HasForeignKey(issue => issue.EpicId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sprint>().WithMany().HasForeignKey(issue => issue.SprintId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(issue => issue.Key).IsUnique();
        builder.HasIndex(issue => new { issue.ProjectId, issue.Number }).IsUnique();
        builder.HasIndex(issue => new { issue.ProjectId, issue.StatusId, issue.Rank });
        builder.HasIndex(issue => new { issue.ProjectId, issue.SprintId, issue.Rank });
        builder.HasIndex(issue => issue.AssigneeId);
        builder.HasIndex(issue => issue.ParentId);
        builder.HasIndex(issue => issue.EpicId);
        builder.HasIndex(issue => issue.DueDate);
        builder.HasIndex(issue => issue.Labels).HasMethod("gin");
    }
}

internal sealed class IssueCommentConfiguration : IEntityTypeConfiguration<IssueComment>
{
    public void Configure(EntityTypeBuilder<IssueComment> builder)
    {
        builder.ToTable("DeliveryIssueComments");
        builder.HasKey(comment => comment.Id);
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.Body).HasMaxLength(IssueComment.BodyMaxLength).IsRequired();
        builder.HasOne<Issue>().WithMany().HasForeignKey(comment => comment.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IssueComment>().WithMany().HasForeignKey(comment => comment.ParentCommentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(comment => comment.IssueId);
    }
}

internal sealed class IssueHistoryEntryConfiguration : IEntityTypeConfiguration<IssueHistoryEntry>
{
    public void Configure(EntityTypeBuilder<IssueHistoryEntry> builder)
    {
        builder.ToTable("DeliveryIssueHistory");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Field).HasMaxLength(IssueHistoryEntry.FieldMaxLength).IsRequired();
        builder.Property(entry => entry.OldValue).HasMaxLength(IssueHistoryEntry.ValueMaxLength);
        builder.Property(entry => entry.NewValue).HasMaxLength(IssueHistoryEntry.ValueMaxLength);
        builder.HasOne<Issue>().WithMany().HasForeignKey(entry => entry.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(entry => entry.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entry => new { entry.IssueId, entry.AtUtc });
        builder.HasIndex(entry => new { entry.ProjectId, entry.AtUtc });
    }
}

internal sealed class IssueLinkConfiguration : IEntityTypeConfiguration<IssueLink>
{
    public void Configure(EntityTypeBuilder<IssueLink> builder)
    {
        builder.ToTable("DeliveryIssueLinks");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedNever();
        builder.Property(link => link.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasOne<Issue>().WithMany().HasForeignKey(link => link.SourceIssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Issue>().WithMany().HasForeignKey(link => link.TargetIssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(link => link.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.SourceIssueId, link.TargetIssueId, link.Type }).IsUnique();
        builder.HasIndex(link => link.TargetIssueId);
    }
}

internal sealed class IssueWatcherConfiguration : IEntityTypeConfiguration<IssueWatcher>
{
    public void Configure(EntityTypeBuilder<IssueWatcher> builder)
    {
        builder.ToTable("DeliveryIssueWatchers");
        builder.HasKey(watcher => watcher.Id);
        builder.Property(watcher => watcher.Id).ValueGeneratedNever();
        builder.HasOne<Issue>().WithMany().HasForeignKey(watcher => watcher.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(watcher => watcher.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(watcher => new { watcher.IssueId, watcher.UserId }).IsUnique();
    }
}

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public const string OneActiveSprintIndex = "ux_delivery_sprints_one_active_per_project";

    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("DeliverySprints");
        builder.HasKey(sprint => sprint.Id);
        builder.Property(sprint => sprint.Id).ValueGeneratedNever();
        builder.Property(sprint => sprint.Name).HasMaxLength(Sprint.NameMaxLength).IsRequired();
        builder.Property(sprint => sprint.Goal).HasMaxLength(Sprint.GoalMaxLength);
        builder.Property(sprint => sprint.RetroWentWell).HasMaxLength(Sprint.RetrospectiveMaxLength);
        builder.Property(sprint => sprint.RetroToImprove).HasMaxLength(Sprint.RetrospectiveMaxLength);
        builder.Property(sprint => sprint.RetroActionItems).HasMaxLength(Sprint.RetrospectiveMaxLength);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(sprint => sprint.RetroUpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.Property(sprint => sprint.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(sprint => sprint.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
        builder.Ignore(sprint => sprint.IsOpen);
        builder.HasOne<DeliveryProject>().WithMany().HasForeignKey(sprint => sprint.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(sprint => new { sprint.ProjectId, sprint.State });

        // At most one active sprint per project, enforced by the database as well as by the service.
        builder.HasIndex(sprint => sprint.ProjectId).IsUnique().HasFilter("state = 'Active'").HasDatabaseName(OneActiveSprintIndex);
    }
}

/// <summary>Issue attachments: rows cascade with their issue (the service removes the stored files); uploaders are kept (restrict).</summary>
internal sealed class IssueAttachmentConfiguration : IEntityTypeConfiguration<IssueAttachment>
{
    public void Configure(EntityTypeBuilder<IssueAttachment> builder)
    {
        builder.ToTable("DeliveryIssueAttachments");
        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).ValueGeneratedNever();
        builder.Property(attachment => attachment.FileName).HasMaxLength(IssueAttachment.FileNameMaxLength).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(IssueAttachment.ContentTypeMaxLength).IsRequired();
        builder.Property(attachment => attachment.Extension).HasMaxLength(IssueAttachment.ExtensionMaxLength).IsRequired();
        builder.Property(attachment => attachment.StorageKey).HasMaxLength(IssueAttachment.StorageKeyMaxLength).IsRequired();
        builder.Property(attachment => attachment.Sha256).HasMaxLength(IssueAttachment.Sha256Length).IsFixedLength().IsRequired();
        builder.Ignore(attachment => attachment.IsImage);
        builder.HasOne<Issue>().WithMany().HasForeignKey(attachment => attachment.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(attachment => attachment.UploadedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(attachment => attachment.IssueId);
        builder.HasIndex(attachment => attachment.StorageKey).IsUnique();
    }
}

/// <summary>Time logged on issues: rows cascade with their issue; the person who logged the time is kept (restrict).</summary>
internal sealed class IssueWorkLogConfiguration : IEntityTypeConfiguration<IssueWorkLog>
{
    public void Configure(EntityTypeBuilder<IssueWorkLog> builder)
    {
        builder.ToTable("DeliveryIssueWorkLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.Note).HasMaxLength(IssueWorkLog.NoteMaxLength);
        builder.HasOne<Issue>().WithMany().HasForeignKey(log => log.IssueId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(log => log.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(log => new { log.IssueId, log.WorkDate });
        builder.HasIndex(log => new { log.UserId, log.WorkDate });
    }
}

/// <summary>Saved issue searches, private to their owner (removed with the owner's account).</summary>
internal sealed class SavedIssueFilterConfiguration : IEntityTypeConfiguration<SavedIssueFilter>
{
    public void Configure(EntityTypeBuilder<SavedIssueFilter> builder)
    {
        builder.ToTable("DeliverySavedIssueFilters");
        builder.HasKey(filter => filter.Id);
        builder.Property(filter => filter.Id).ValueGeneratedNever();
        builder.Property(filter => filter.Name).HasMaxLength(SavedIssueFilter.NameMaxLength).IsRequired();
        builder.Property(filter => filter.Query).HasMaxLength(SavedIssueFilter.QueryMaxLength).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(filter => filter.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(filter => new { filter.OwnerId, filter.CreatedAtUtc });
    }
}
