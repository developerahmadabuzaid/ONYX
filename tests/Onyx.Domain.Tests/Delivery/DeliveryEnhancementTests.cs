using Onyx.Domain.Delivery;

namespace Onyx.Domain.Tests.Delivery;

/// <summary>Rules for time logs, attachments' names, saved filters, retrospectives and rescheduling. Fictional sample data.</summary>
public sealed class DeliveryEnhancementTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 25);
    private static readonly Guid Owner = Guid.CreateVersion7();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1440, true)]
    [InlineData(1441, false)]
    public void Time_entries_are_between_one_minute_and_one_day(int minutes, bool valid) =>
        Assert.Equal(valid, IssueWorkLog.Create(Issue(), Owner, minutes, Today, null, Today, Now).IsSuccess);

    [Fact]
    public void Time_cannot_be_logged_for_the_future_or_the_distant_past()
    {
        Assert.True(IssueWorkLog.Create(Issue(), Owner, 30, Today.AddDays(1), null, Today, Now).IsSuccess);
        Assert.Equal("Delivery.InvalidWorkLogDate", IssueWorkLog.Create(Issue(), Owner, 30, Today.AddDays(2), null, Today, Now).Error!.Code);
        Assert.Equal("Delivery.InvalidWorkLogDate", IssueWorkLog.Create(Issue(), Owner, 30, Today.AddYears(-3), null, Today, Now).Error!.Code);
        Assert.Equal("Delivery.InvalidWorkLogNote", IssueWorkLog.Create(Issue(), Owner, 30, Today, new string('x', 501), Today, Now).Error!.Code);
    }

    [Theory]
    [InlineData("report.pdf", "pdf", "report.pdf")]
    [InlineData("../../etc/notes.txt", "txt", "notes.txt")]
    [InlineData("C:\\Users\\sample\\scan.PNG", "png", "scan.png")]
    [InlineData("invoice.pdf.html", "txt", "invoice.pdf.html.txt")]
    [InlineData("<script>.png", "png", "script.png")]
    [InlineData("", "pdf", "file.pdf")]
    [InlineData(null, "csv", "file.csv")]
    public void Attachment_names_lose_paths_and_always_carry_the_detected_extension(string? original, string extension, string expected) =>
        Assert.Equal(expected, IssueAttachment.SafeFileName(original, extension));

    [Fact]
    public void Attachment_names_are_length_limited()
    {
        var name = IssueAttachment.SafeFileName(new string('a', 400) + ".pdf", "pdf");
        Assert.Equal(IssueAttachment.FileNameMaxLength, name.Length);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("type=Bug&overdue=true", "overdue=true&type=Bug")]
    [InlineData("?status=Open&text=sample%20bug&label=", "status=Open&text=sample%20bug")]
    [InlineData("PROJECTID=abc", "PROJECTID=abc")]
    public void Saved_filters_keep_known_keys_in_a_stable_order(string query, string expected) =>
        Assert.Equal(expected, SavedIssueFilter.Create(Owner, "Sample", query, Now).Value!.Query);

    [Theory]
    [InlineData("")]
    [InlineData("drop=table")]
    [InlineData("type=Bug&type=Task")]
    [InlineData("type")]
    [InlineData("text=two words")]
    [InlineData("label=")]
    public void Saved_filters_refuse_anything_else(string query) =>
        Assert.Equal("Delivery.InvalidFilterQuery", SavedIssueFilter.Create(Owner, "Sample", query, Now).Error!.Code);

    [Fact]
    public void A_retrospective_is_recorded_once_the_sprint_started()
    {
        var sprint = Sprint.Create(Guid.CreateVersion7(), "Sprint 1", null, Today, Today.AddDays(13)).Value!;
        Assert.Equal("Delivery.RetrospectiveTooEarly", sprint.RecordRetrospective("Good", null, null, Owner, Now).Error!.Code);

        Assert.True(sprint.Start(2, 8, Now).IsSuccess);
        Assert.True(sprint.RecordRetrospective(" Pairing worked ", "Smaller stories", null, Owner, Now).IsSuccess);
        Assert.Equal("Pairing worked", sprint.RetroWentWell);
        Assert.Equal(Owner, sprint.RetroUpdatedById);
        Assert.Equal("Delivery.InvalidRetrospective", sprint.RecordRetrospective(new string('x', 4001), null, null, Owner, Now).Error!.Code);
    }

    [Fact]
    public void Rescheduling_keeps_the_dates_in_order_and_reports_changes()
    {
        var issue = Issue();
        var changes = issue.Reschedule(Today, Today.AddDays(3));
        Assert.Equal(["startDate", "dueDate"], changes.Value!.Select(change => change.Field));
        Assert.Equal(Today.AddDays(3), issue.DueDate);
        Assert.Empty(issue.Reschedule(Today, Today.AddDays(3)).Value!);
        Assert.Equal("Delivery.InvalidIssueDates", issue.Reschedule(Today.AddDays(4), Today).Error!.Code);
    }

    private static Issue Issue()
    {
        var details = new DeliveryProjectDetails("Sample project", null, DeliveryProjectType.AiAgent, DeliveryClassification.Internal, null, DeliveryProjectStatus.Active, DeliveryPriority.High, null, null, null);
        var project = DeliveryProject.Create("SMP", Owner, details, Now).Value!;
        return Domain.Delivery.Issue.Create(project, IssueType.Task, Owner, new IssueDetails("Sample task", null, DeliveryPriority.Medium, null, null, null, null, null, null, null, null, null, null, null), 1024).Value!;
    }
}
