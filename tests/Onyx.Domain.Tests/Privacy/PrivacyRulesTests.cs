using Onyx.Domain.Booking;
using Onyx.Domain.Leads;
using Onyx.Domain.Privacy;

namespace Onyx.Domain.Tests.Privacy;

public sealed class PrivacyRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_request_runs_only_after_verification_and_only_for_its_own_type()
    {
        var request = DataSubjectRequest.Receive(DataSubjectRequestType.Erasure, " Subject@Example.com ", "By email", Now).Value;
        Assert.Equal("subject@example.com", request.SubjectEmail);
        Assert.Equal(DataSubjectRequest.HashEmail("subject@example.com"), request.SubjectEmailHash);

        Assert.True(request.EnsureExecutable(DataSubjectRequestType.Erasure).IsFailure);
        Assert.True(request.Verify(Guid.NewGuid(), " ", Now).IsFailure);
        Assert.True(request.Verify(Guid.NewGuid(), "Replied from the same address.", Now).IsSuccess);
        Assert.True(request.Verify(Guid.NewGuid(), "Again", Now).IsFailure);
        Assert.True(request.EnsureExecutable(DataSubjectRequestType.Erasure).IsSuccess);
        Assert.True(request.EnsureExecutable(DataSubjectRequestType.Export).IsFailure);

        request.Complete(Guid.NewGuid(), "{}", Now);
        request.ForgetSubject();
        Assert.Null(request.SubjectEmail);
        Assert.Null(request.RequestNote);
        Assert.Equal("[erased]", request.VerificationNote);
        Assert.True(request.Reject(Guid.NewGuid(), "late", Now).IsFailure);
    }

    [Fact]
    public void A_completed_export_can_be_downloaded_again_until_the_subject_is_erased()
    {
        var request = DataSubjectRequest.Receive(DataSubjectRequestType.Export, "a@example.com", null, Now).Value;
        request.Verify(Guid.NewGuid(), "Checked.", Now);
        request.Complete(Guid.NewGuid(), "{}", Now);

        Assert.True(request.EnsureExecutable(DataSubjectRequestType.Export).IsSuccess);
        request.ForgetSubject();
        Assert.True(request.EnsureExecutable(DataSubjectRequestType.Export).IsFailure);
    }

    [Fact]
    public void Requests_need_a_valid_address_and_a_known_type()
    {
        Assert.True(DataSubjectRequest.Receive(DataSubjectRequestType.Export, "not-an-email", null, Now).IsFailure);
        Assert.True(DataSubjectRequest.Receive((DataSubjectRequestType)9, "a@example.com", null, Now).IsFailure);
        Assert.True(DataSubjectRequest.Receive(DataSubjectRequestType.Export, "a@example.com", new string('x', 1_001), Now).IsFailure);
    }

    [Fact]
    public void Retention_policies_have_no_default_period_and_enable_only_with_one()
    {
        Assert.True(RetentionPolicy.Create(RetentionCategory.AuditLogs, RetentionAction.Delete, null, isEnabled: true).IsFailure);
        Assert.True(RetentionPolicy.Create(RetentionCategory.EmailMessages, RetentionAction.Anonymize, 30, isEnabled: false).IsFailure);
        Assert.True(RetentionPolicy.Create(RetentionCategory.AuditLogs, RetentionAction.Delete, 0, isEnabled: false).IsFailure);

        var policy = RetentionPolicy.Create(RetentionCategory.LeadsAndInquiries, RetentionAction.Anonymize, 30, isEnabled: false).Value;
        Assert.False(policy.IsActive);
        Assert.Null(policy.CutoffAt(Now));

        Assert.True(policy.Update(RetentionAction.Anonymize, 30, isEnabled: true).IsSuccess);
        Assert.Equal(Now.AddDays(-30), policy.CutoffAt(Now));
    }

    [Fact]
    public void Erasing_keeps_only_non_personal_fields_and_is_idempotent()
    {
        var contact = ContactDetails.Create("سارة", "sara@example.com", "+966 1", "شركة", "SA").Value;
        var attribution = Attribution.Create(
            new TouchPoint("/ar", "https://ref.example/path", "google", "cpc", "launch", "ad", "term", Now), null, "/ar/x", "ar", "SA", "mobile", "1.2", true);
        var lead = Lead.Open(contact, InquiryType.Contact, null, "ar", Now);
        var inquiry = Inquiry.Submit(InquiryType.Contact, "ar", contact, "رسالة", InquiryAnswers.Empty, null, null, null, attribution, lead.Id, Now).Value;

        inquiry.Erase(Now);
        lead.Erase(null, Now);
        inquiry.Erase(Now.AddDays(1));
        lead.Erase(null, Now.AddDays(1));

        Assert.Equal(ErasedValues.Name, inquiry.Name);
        Assert.Equal(ErasedValues.Email(inquiry.Id), inquiry.NormalizedEmail);
        Assert.Null(inquiry.Message);
        Assert.Null(inquiry.Phone);
        Assert.Null(inquiry.Attribution.AnalyticsClientId);
        Assert.Null(inquiry.Attribution.SubmissionPath);
        Assert.Equal("google", inquiry.Attribution.FirstTouch!.UtmSource);
        Assert.Null(inquiry.Attribution.FirstTouch.Referrer);
        Assert.Null(inquiry.Attribution.FirstTouch.UtmTerm);
        Assert.Equal(Now, inquiry.ErasedAtUtc);
        Assert.Equal(ErasedValues.Name, lead.DisplayName);
        Assert.Null(lead.Company);
        Assert.Single(lead.PendingActivities, activity => activity.Kind == LeadActivityKind.Erased);
    }

    [Fact]
    public void Erasing_a_visitor_cancels_upcoming_bookings_and_disables_the_manage_link()
    {
        var hash = new string('a', ConsultationBooking.HashLength);
        var upcoming = ConsultationBooking.Confirm(Now.AddDays(1), Now.AddDays(1).AddMinutes(30), "Asia/Riyadh", Guid.NewGuid(), Guid.NewGuid(), "en", hash, Now.AddDays(2), null, Now).Value;
        var past = ConsultationBooking.Confirm(Now.AddDays(1), Now.AddDays(1).AddMinutes(30), "Asia/Riyadh", Guid.NewGuid(), Guid.NewGuid(), "en", hash, Now.AddDays(5), null, Now).Value;

        upcoming.EraseVisitorAccess(Now);
        past.EraseVisitorAccess(Now.AddDays(2));

        Assert.Equal(BookingStatus.Cancelled, upcoming.Status);
        Assert.Null(upcoming.ManageTokenHash);
        Assert.Equal(BookingStatus.Confirmed, past.Status);
        Assert.Null(past.ManageTokenHash);
    }
}
