using FluentValidation;
using Onyx.Application.Localization;
using Onyx.Contracts.Privacy;

namespace Onyx.Application.Privacy;

internal sealed class RecordCookieConsentRequestValidator : AbstractValidator<RecordCookieConsentRequest>
{
    public RecordCookieConsentRequestValidator()
    {
        RuleFor(request => request.ConsentId).NotEmpty();
        RuleFor(request => request.PolicyVersion).NotEmpty().MaximumLength(20).Matches("^[A-Za-z0-9._-]+$");
        RuleFor(request => request.LanguageCode).Must(SupportedLanguages.All.Contains).WithMessage("Unsupported language.");
    }
}
