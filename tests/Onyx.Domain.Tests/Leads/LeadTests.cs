using Onyx.Domain.Leads;

namespace Onyx.Domain.Tests.Leads;

public sealed class LeadTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Closing_and_reopening_a_lead_tracks_closure_and_records_activities()
    {
        var lead = NewLead();
        var actor = Guid.NewGuid();

        Assert.True(lead.ChangeStage(LeadStage.Won, actor, Now).IsSuccess);
        Assert.False(lead.IsOpen);
        Assert.Equal(Now, lead.ClosedAtUtc);

        Assert.True(lead.ChangeStage(LeadStage.Qualified, actor, Now.AddDays(1)).IsSuccess);
        Assert.True(lead.IsOpen);
        Assert.Null(lead.ClosedAtUtc);
        Assert.Equal(2, lead.PendingActivities.Count(activity => activity.Kind == LeadActivityKind.StageChanged));
        Assert.True(lead.ChangeStage((LeadStage)99, actor, Now).IsFailure);
    }

    [Fact]
    public void Unchanged_values_record_no_activity()
    {
        var lead = NewLead();
        lead.ClearPendingActivities();

        lead.ChangeStage(LeadStage.New, Guid.NewGuid(), Now);
        lead.ChangePriority(LeadPriority.Normal, Guid.NewGuid(), Now);
        lead.AssignTo(null, Guid.NewGuid(), Now);

        Assert.Empty(lead.PendingActivities);
    }

    [Fact]
    public void The_score_is_rule_based_capped_and_explained()
    {
        var contact = ContactDetails.Create("Sample", "a@example.com", "+966 1", "Sample Co", null).Value;
        var lead = Lead.Open(contact, InquiryType.Consultation, null, "en", Now);
        var answers = InquiryAnswers.Create(1, new Dictionary<string, IReadOnlyList<string>> { ["a"] = ["1"], ["b"] = ["2"], ["c"] = ["3"] }).Value;
        var attribution = Attribution.Create(null, null, null, "en", null, null, null, false);
        var booked = Inquiry.Submit(InquiryType.Consultation, "en", contact, null, answers, null, null, null, attribution, lead.Id, Now).Value;
        var brief = Inquiry.Submit(InquiryType.StartProject, "en", contact, "Brief", answers, null, null, null, attribution, lead.Id, Now).Value;

        var (score, reasons) = LeadScore.Evaluate([booked, brief]);

        Assert.Equal(90, score);
        Assert.Equal(["booked-consultation", "project-or-consultation-request", "company-provided", "phone-provided", "detailed-brief", "repeat-inquiry"], reasons);
        Assert.True(score <= LeadScore.MaxScore);
    }

    [Fact]
    public void Notes_are_bounded_and_never_empty()
    {
        Assert.True(LeadNote.Write(Guid.NewGuid(), " ", Guid.NewGuid(), Now).IsFailure);
        Assert.True(LeadNote.Write(Guid.NewGuid(), new string('x', LeadNote.BodyMaxLength + 1), Guid.NewGuid(), Now).IsFailure);
        Assert.Equal("Call back", LeadNote.Write(Guid.NewGuid(), " Call back ", Guid.NewGuid(), Now).Value.Body);
    }

    private static Lead NewLead() =>
        Lead.Open(ContactDetails.Create("Sample", "a@example.com", null, null, null).Value, InquiryType.Contact, null, "en", Now);
}
