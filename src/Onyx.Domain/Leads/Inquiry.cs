using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Leads;

/// <summary>
/// One submission, immutable once stored (architecture.md §13.2): who sent it, what they need, the page/service/project it came
/// from (a booked consultation references its inquiry), its attribution and the time the anti-abuse check passed. Its <see cref="ConsentRecord"/> is kept separately because
/// marketing consent can be withdrawn later.
/// </summary>
public sealed class Inquiry : Entity
{
    public const int MessageMaxLength = 4_000;

    private Inquiry()
    {
        LanguageCode = string.Empty;
        Name = string.Empty;
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        Answers = InquiryAnswers.Empty;
        Attribution = new Attribution(null, null, null, string.Empty, null, null, null, null, null, false);
    }

    public InquiryType Type { get; private set; }

    public string LanguageCode { get; private set; }

    [SensitiveData]
    public string Name { get; private set; }

    [SensitiveData]
    public string Email { get; private set; }

    [SensitiveData]
    public string NormalizedEmail { get; private set; }

    [SensitiveData]
    public string? Phone { get; private set; }

    public string? Company { get; private set; }

    public string? Country { get; private set; }

    [SensitiveData]
    public string? Message { get; private set; }

    public InquiryAnswers Answers { get; private set; }

    /// <summary>The service, project or call to action the inquiry was made from, when there was one.</summary>
    public Guid? ServiceId { get; private set; }

    public Guid? ProjectId { get; private set; }

    public Guid? CallToActionId { get; private set; }

    public Attribution Attribution { get; private set; }

    public Guid LeadId { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    /// <summary>When the anti-abuse (Turnstile) check passed for this submission.</summary>
    public DateTimeOffset VerifiedAtUtc { get; private set; }

    /// <summary>
    /// Per-type requirements: free-text requests need a message; service and similar-project inquiries need what they refer to.
    /// Exposed so intake can refuse before spending the single-use anti-abuse token.
    /// </summary>
    public static Result CheckRequirements(InquiryType type, string? message, Guid? serviceId, Guid? projectId)
    {
        var requiresMessage = type is InquiryType.Contact or InquiryType.ServiceInquiry or InquiryType.SimilarProject or InquiryType.RequestConsultation;
        if (requiresMessage && string.IsNullOrWhiteSpace(message))
        {
            return Error.Validation("Inquiry.MessageRequired", "Tell us briefly what you need.");
        }

        if ((type == InquiryType.ServiceInquiry && serviceId is null) || (type == InquiryType.SimilarProject && projectId is null))
        {
            return Error.Validation("Inquiry.MissingContext", "This kind of inquiry needs the service or project it refers to.");
        }

        return Result.Success();
    }

    public static Result<Inquiry> Submit(
        InquiryType type, string languageCode, ContactDetails contact, string? message, InquiryAnswers answers, Guid? serviceId, Guid? projectId,
        Guid? callToActionId, Attribution attribution, Guid leadId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(contact);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(attribution);
        var cleanMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (cleanMessage is { Length: > MessageMaxLength } || (cleanMessage is not null && ContentBodyRules.HasControlCharacters(cleanMessage, allowNewLines: true)))
        {
            return Error.Validation("Inquiry.InvalidMessage", $"Messages are limited to {MessageMaxLength} characters.");
        }

        var requirements = CheckRequirements(type, cleanMessage, serviceId, projectId);
        if (requirements.IsFailure)
        {
            return requirements.Error!;
        }

        var inquiry = new Inquiry
        {
            Type = type,
            LanguageCode = languageCode,
            Name = contact.Name,
            Email = contact.Email,
            NormalizedEmail = contact.NormalizedEmail,
            Phone = contact.Phone,
            Company = contact.Company,
            Country = contact.Country,
            Message = cleanMessage,
            Answers = answers,
            ServiceId = serviceId,
            ProjectId = projectId,
            CallToActionId = callToActionId,
            Attribution = attribution,
            LeadId = leadId,
            SubmittedAtUtc = now,
            VerifiedAtUtc = now,
        };
        inquiry.RaiseDomainEvent(new InquirySubmitted(inquiry.Id, leadId));
        return inquiry;
    }
}
