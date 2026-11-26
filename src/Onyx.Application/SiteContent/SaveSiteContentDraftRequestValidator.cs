using FluentValidation;
using Onyx.Contracts.SiteContent;
using Onyx.Domain.SiteContent;

namespace Onyx.Application.SiteContent;

internal sealed class SaveSiteContentDraftRequestValidator : AbstractValidator<SaveSiteContentDraftRequest>
{
    public SaveSiteContentDraftRequestValidator()
    {
        RuleFor(request => request.Json).NotEmpty().MaximumLength(SiteContentDocument.JsonMaxLength);
    }
}
