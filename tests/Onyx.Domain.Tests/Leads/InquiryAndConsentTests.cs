using Onyx.Domain.Leads;

namespace Onyx.Domain.Tests.Leads;

public sealed class InquiryAndConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("", "a@example.com", null)]
    [InlineData("Name", "not-an-email", null)]
    [InlineData("Name", "a @example.com", null)]
    [InlineData("Name", "a@example.com", "+966 abc")]
    public void Contact_details_reject_invalid_values(string name, string email, string? phone) =>
        Assert.True(ContactDetails.Create(name, email, phone, null, null).IsFailure);

    [Fact]
    public void Contact_details_are_trimmed_and_the_email_normalized()
    {
        var contact = ContactDetails.Create("  Sample  ", " Visitor@Example.COM ", " +966 (11) 000-0000 ", " ", "sa").Value;

        Assert.Equal("Sample", contact.Name);
        Assert.Equal("visitor@example.com", contact.NormalizedEmail);
        Assert.Equal("+966 (11) 000-0000", contact.Phone);
        Assert.Null(contact.Company);
        Assert.Equal("SA", contact.Country);
    }

    [Theory]
    [InlineData(InquiryType.Contact, null, null, null)]
    [InlineData(InquiryType.RequestConsultation, "  ", null, null)]
    [InlineData(InquiryType.ServiceInquiry, "Need help", null, null)]
    [InlineData(InquiryType.SimilarProject, "Like this one", null, null)]
    public void Per_type_requirements_are_enforced(InquiryType type, string? message, string? service, string? project) =>
        Assert.True(Inquiry.CheckRequirements(type, message, service is null ? null : Guid.Parse(service), project is null ? null : Guid.Parse(project)).IsFailure);

    [Fact]
    public void A_project_brief_needs_no_free_text_and_raises_the_submission_event()
    {
        var contact = ContactDetails.Create("Sample", "a@example.com", null, null, null).Value;
        var attribution = Attribution.Create(null, null, "/en/start-a-project", "en", null, "desktop", null, false);
        var leadId = Guid.CreateVersion7();

        var inquiry = Inquiry.Submit(InquiryType.StartProject, "en", contact, null, InquiryAnswers.Empty, null, null, null, attribution, leadId, Now).Value;

        var submitted = Assert.IsType<InquirySubmitted>(Assert.Single(inquiry.DomainEvents));
        Assert.Equal(inquiry.Id, submitted.InquiryId);
        Assert.Equal(leadId, submitted.LeadId);
        Assert.Equal(Now, inquiry.VerifiedAtUtc);
    }

    [Fact]
    public void Answers_are_bounded_and_cleaned()
    {
        Assert.True(InquiryAnswers.Create(1, new Dictionary<string, IReadOnlyList<string>> { ["bad key!"] = ["x"] }).IsFailure);
        Assert.True(InquiryAnswers.Create(0, null).IsFailure);
        Assert.True(InquiryAnswers.Create(1, new Dictionary<string, IReadOnlyList<string>> { ["pillars"] = [new string('x', 201)] }).IsFailure);

        var answers = InquiryAnswers.Create(1, new Dictionary<string, IReadOnlyList<string>> { ["pillars"] = [" ai ", "ai", ""], ["empty"] = [" "] }).Value;
        Assert.Equal(["ai"], answers.Values["pillars"]);
        Assert.False(answers.Values.ContainsKey("empty"));
    }

    [Fact]
    public void Contact_consent_is_required_and_marketing_consent_is_separate_and_withdrawable()
    {
        Assert.True(ConsentRecord.Give(Guid.NewGuid(), Guid.NewGuid(), contactConsent: false, true, "1", "en", Now).IsFailure);

        var consent = ConsentRecord.Give(Guid.NewGuid(), Guid.NewGuid(), contactConsent: true, marketingConsent: true, "1", "en", Now).Value;
        Assert.True(consent.HasMarketingConsent);
        Assert.True(consent.WithdrawMarketing(Guid.NewGuid(), Now.AddDays(1)).IsSuccess);
        Assert.False(consent.HasMarketingConsent);
        Assert.True(consent.ContactConsent);
        Assert.True(consent.WithdrawMarketing(null, Now.AddDays(2)).IsFailure);

        var contactOnly = ConsentRecord.Give(Guid.NewGuid(), Guid.NewGuid(), true, false, "1", "en", Now).Value;
        Assert.True(contactOnly.WithdrawMarketing(null, Now).IsFailure);
    }
}
