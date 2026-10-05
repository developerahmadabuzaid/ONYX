using FluentValidation;
using Onyx.Application.Localization;
using Onyx.Contracts.Leads;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>Structural limits of a public form. The domain applies the content rules (email format, phone characters, etc.).</summary>
internal sealed class InquiryFormValidator : AbstractValidator<InquiryFormDto>
{
    public const int CaptchaTokenMaxLength = 2_048;

    public InquiryFormValidator()
    {
        RuleFor(form => form.LanguageCode).Must(language => language is not null && SupportedLanguages.All.Contains(language)).WithMessage("Unsupported language.");
        RuleFor(form => form.Name).NotEmpty().MaximumLength(ContactDetails.NameMaxLength);
        RuleFor(form => form.Email).NotEmpty().MaximumLength(ContactDetails.EmailMaxLength);
        RuleFor(form => form.Phone).MaximumLength(ContactDetails.PhoneMaxLength);
        RuleFor(form => form.Company).MaximumLength(ContactDetails.CompanyMaxLength);
        RuleFor(form => form.Country).MaximumLength(2);
        RuleFor(form => form.Message).MaximumLength(Inquiry.MessageMaxLength);
        RuleFor(form => form.AnswersVersion).InclusiveBetween(1, 100);
        RuleFor(form => form.Answers).Must(answers => answers is null || answers.Count <= InquiryAnswers.MaxKeys).WithMessage("Too many answers.");
        RuleFor(form => form.CaptchaToken).MaximumLength(CaptchaTokenMaxLength);
        RuleFor(form => form.Website).MaximumLength(500);
        RuleFor(form => form.Attribution!.SubmissionPath).MaximumLength(TouchPoint.PathMaxLength).When(form => form.Attribution is not null);
        RuleFor(form => form.Attribution!.AnalyticsClientId).MaximumLength(Attribution.ClientIdMaxLength).When(form => form.Attribution is not null);
    }
}
