using Onyx.Domain.Delivery;

namespace Onyx.Domain.Tests.Delivery;

public sealed class DeliveryDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.CreateVersion7();

    [Theory]
    [InlineData("AI", true)]
    [InlineData("WEB2", true)]
    [InlineData("A", false)]
    [InlineData("1AI", false)]
    [InlineData("TOOLONGKEY1", false)]
    [InlineData("A-I", false)]
    public void Project_keys_are_short_upper_case_identifiers(string key, bool valid) =>
        Assert.Equal(valid, DeliveryProject.Create(key, Owner, Details(), Now).IsSuccess);

    [Fact]
    public void A_new_project_has_the_default_workflow_and_its_owner_as_admin()
    {
        var project = Project();
        Assert.Equal(["backlog", "todo", "in-progress", "in-review", "testing", "done"], project.Statuses.OrderBy(status => status.Order).Select(status => status.SystemKey));
        Assert.Equal(ProjectRole.Admin, project.RoleOf(Owner));
        Assert.Equal("backlog", project.InitialStatus.SystemKey);

        var backlog = Status(project, "backlog");
        Assert.True(project.CanTransition(backlog.Id, Status(project, "todo").Id));
        Assert.False(project.CanTransition(backlog.Id, Status(project, "done").Id));
        Assert.True(project.CanTransition(Status(project, "done").Id, Status(project, "todo").Id));
    }

    [Fact]
    public void A_project_keeps_at_least_one_admin()
    {
        var project = Project();
        Assert.Equal("Delivery.LastProjectAdmin", project.ChangeMemberRole(Owner, ProjectRole.Developer).Error!.Code);
        Assert.Equal("Delivery.LastProjectAdmin", project.RemoveMember(Owner).Error!.Code);

        var other = Guid.CreateVersion7();
        Assert.True(project.AddMember(other, ProjectRole.Admin, Now).IsSuccess);
        Assert.True(project.ChangeMemberRole(Owner, ProjectRole.Viewer).IsSuccess);
        Assert.Equal("Delivery.AlreadyMember", project.AddMember(other, ProjectRole.Developer, Now).Error!.Code);
    }

    [Fact]
    public void Client_projects_name_their_client()
    {
        var details = Details() with { Classification = DeliveryClassification.Client, ClientName = null };
        Assert.Equal("Delivery.ClientNameRequired", DeliveryProject.Create("CLI", Owner, details, Now).Error!.Code);
    }

    [Fact]
    public void Issue_numbers_grow_and_keys_combine_project_and_number()
    {
        var project = Project();
        var first = Issue(project, IssueType.Task);
        var second = Issue(project, IssueType.Bug);
        Assert.Equal("AI-1", first.Key);
        Assert.Equal("AI-2", second.Key);
        Assert.Equal(3, project.NextIssueNumber);
    }

    [Fact]
    public void Hierarchy_rules_keep_subtasks_and_epics_consistent()
    {
        var project = Project();
        var epic = Issue(project, IssueType.Epic);
        var story = Issue(project, IssueType.Story);
        var subtask = Issue(project, IssueType.Subtask);

        Assert.Equal("Delivery.SubtaskNeedsParent", subtask.SetHierarchy(null, null).Error!.Code);
        Assert.Equal("Delivery.InvalidParent", subtask.SetHierarchy(epic, null).Error!.Code);
        Assert.True(subtask.SetHierarchy(story, null).IsSuccess);
        Assert.Equal("Delivery.ParentOnlyForSubtasks", story.SetHierarchy(epic, null).Error!.Code);
        Assert.Equal("Delivery.InvalidEpic", story.SetHierarchy(null, story).Error!.Code);
        Assert.Equal("Delivery.InvalidEpic", epic.SetHierarchy(null, epic).Error!.Code);
        Assert.True(story.SetHierarchy(null, epic).IsSuccess);
        Assert.Equal(epic.Id, story.EpicId);

        Assert.Equal("Delivery.TypeChangeNotAllowed", epic.ChangeType(IssueType.Task).Error!.Code);
        Assert.Equal("Delivery.TypeChangeNotAllowed", story.ChangeType(IssueType.Subtask).Error!.Code);
        Assert.Equal("type", story.ChangeType(IssueType.Feature).Value!.Field);
    }

    [Fact]
    public void Moving_to_done_records_resolution_and_reopening_clears_it()
    {
        var project = Project();
        var issue = Issue(project, IssueType.Task);
        Assert.NotNull(issue.MoveTo(Status(project, "done"), Now));
        Assert.Equal(Now, issue.ResolvedAtUtc);
        issue.MoveTo(Status(project, "todo"), Now.AddHours(1));
        Assert.Null(issue.ResolvedAtUtc);
        Assert.Null(issue.MoveTo(Status(project, "todo"), Now.AddHours(2)));
    }

    [Fact]
    public void Issue_updates_report_changes_and_validate_input()
    {
        var project = Project();
        var issue = Issue(project, IssueType.Task);
        var changes = issue.Update(IssueDetails("Renamed") with { StoryPoints = 5, Labels = ["Backend", "backend", "api"] }).Value!;
        Assert.Contains(changes, change => change.Field == "title" && change.NewValue == "Renamed");
        Assert.Contains(changes, change => change.Field == "storyPoints" && change.NewValue == "5");
        Assert.Equal(["backend", "api"], issue.Labels);

        Assert.Equal("Delivery.InvalidStoryPoints", issue.Update(IssueDetails("x") with { StoryPoints = 101 }).Error!.Code);
        Assert.Equal("Delivery.InvalidLabels", issue.Update(IssueDetails("x") with { Labels = ["two words"] }).Error!.Code);
        Assert.Equal("Delivery.InvalidUrl", issue.Update(IssueDetails("x") with { PullRequestUrl = "javascript:alert(1)" }).Error!.Code);
        Assert.Equal("Delivery.InvalidIssueDates", issue.Update(IssueDetails("x") with { StartDate = new DateOnly(2026, 10, 10), DueDate = new DateOnly(2026, 10, 1) }).Error!.Code);
    }

    [Fact]
    public void Sprints_follow_their_lifecycle()
    {
        var start = new DateOnly(2026, 11, 2);
        Assert.Equal("Delivery.InvalidSprintDates", Sprint.Create(Guid.NewGuid(), "S1", null, start, start.AddDays(-1)).Error!.Code);
        Assert.Equal("Delivery.InvalidSprintDates", Sprint.Create(Guid.NewGuid(), "S1", null, start, start.AddDays(61)).Error!.Code);

        var undated = Sprint.Create(Guid.NewGuid(), "Undated", null, null, null).Value!;
        Assert.Equal("Delivery.SprintDatesRequired", undated.Start(1, 1, Now).Error!.Code);

        var sprint = Sprint.Create(Guid.NewGuid(), "S1", "Goal", start, start.AddDays(13)).Value!;
        Assert.Equal("Delivery.SprintEmpty", sprint.Start(0, 0, Now).Error!.Code);
        Assert.Equal("Delivery.SprintNotActive", sprint.Complete(0, 0, Now).Error!.Code);
        Assert.True(sprint.Start(3, 8, Now).IsSuccess);
        Assert.Equal("Delivery.SprintNotPlanned", sprint.Start(3, 8, Now).Error!.Code);
        Assert.True(sprint.Complete(2, 5, Now.AddDays(14)).IsSuccess);
        Assert.Equal((8, 5), (sprint.CommittedPoints, sprint.CompletedPoints));
        Assert.Equal("Delivery.SprintClosed", sprint.Cancel(Now).Error!.Code);
        Assert.Equal("Delivery.SprintClosed", sprint.Plan("Renamed", null, start, start.AddDays(1)).Error!.Code);
    }

    [Fact]
    public void Comments_validate_replies_and_keep_a_placeholder_when_deleted()
    {
        var issueId = Guid.NewGuid();
        var top = IssueComment.Create(issueId, Owner, "First", null).Value!;
        var reply = IssueComment.Create(issueId, Owner, "Reply", top).Value!;
        Assert.Equal("Delivery.InvalidReply", IssueComment.Create(issueId, Owner, "Nested", reply).Error!.Code);
        Assert.Equal("Delivery.InvalidReply", IssueComment.Create(Guid.NewGuid(), Owner, "Elsewhere", top).Error!.Code);
        Assert.Equal("Delivery.InvalidComment", IssueComment.Create(issueId, Owner, "   ", null).Error!.Code);

        top.Delete();
        Assert.True(top.IsDeleted);
        Assert.Equal(string.Empty, top.Body);
        Assert.Equal("Delivery.CommentDeleted", top.Edit("Again", Now).Error!.Code);
    }

    private static DeliveryProjectDetails Details() =>
        new("Sample AI project", null, DeliveryProjectType.AiAgent, DeliveryClassification.Internal, null, DeliveryProjectStatus.Active, DeliveryPriority.High, null, null, null);

    private static DeliveryProject Project() => DeliveryProject.Create("AI", Owner, Details(), Now).Value!;

    private static IssueDetails IssueDetails(string title) =>
        new(title, null, DeliveryPriority.Medium, null, null, null, null, null, null, null, null, null, null, null);

    private static Issue Issue(DeliveryProject project, IssueType type) =>
        Domain.Delivery.Issue.Create(project, type, Owner, IssueDetails($"Sample {type}"), 1024).Value!;

    private static WorkflowStatus Status(DeliveryProject project, string key) => project.Statuses.Single(status => status.SystemKey == key);
}
