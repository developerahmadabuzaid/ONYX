using FluentValidation;
using Onyx.Application.Abstractions;
using Onyx.Application.Common;
using Onyx.Application.Content;
using Onyx.Contracts.Leads;
using Onyx.Domain.Common;
using Onyx.Domain.Content;
using Onyx.Domain.Leads;

namespace Onyx.Application.Leads;

/// <summary>
/// The shared front half of every public submission (forms and bookings), in a fixed order: structural validation → honeypot →
/// domain validation of contact, answers and context → consent → server-side Turnstile verification. Turnstile runs last so a
/// visitor who mistyped a field keeps a valid challenge only when nothing else is wrong (tokens are single use).
/// </summary>
public sealed class InquiryIntake(
    IValidator<InquiryFormDto> validator,
    ICaptchaVerifier captcha,
    IRequestContext requestContext,
    IContentRepository content,
    ICallToActionRepository ctas)
{
    private static readonly Error InvalidContext = Error.Validation("Inquiry.InvalidContext", "The referenced service, project or call to action is not available.");

    internal async Task<Result<PreparedInquiry>> PrepareAsync(InquiryType type, InquiryFormDto form, string captchaAction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (await validator.ValidateToErrorAsync(form, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        // Honeypot: bots fill every field. Pretend success (no signal to tune against) and store nothing.
        if (!string.IsNullOrWhiteSpace(form.Website))
        {
            return PreparedInquiry.Spam(type, form.LanguageCode);
        }

        var contact = ContactDetails.Create(form.Name, form.Email, form.Phone, form.Company, form.Country);
        if (contact.IsFailure)
        {
            return contact.Error!;
        }

        var answers = InquiryAnswers.Create(form.AnswersVersion, form.Answers);
        if (answers.IsFailure)
        {
            return answers.Error!;
        }

        if (!form.ContactConsent)
        {
            return Error.Validation("Inquiry.ContactConsentRequired", "Please agree to be contacted about your request.");
        }

        var requirements = Inquiry.CheckRequirements(type, form.Message, form.ServiceId, form.ProjectId);
        if (requirements.IsFailure)
        {
            return requirements.Error!;
        }

        var context = await ResolveContextAsync(type, form, cancellationToken).ConfigureAwait(false);
        if (context.IsFailure)
        {
            return context.Error!;
        }

        var verified = await captcha.VerifyAsync(form.CaptchaToken, captchaAction, requestContext.IpAddress, cancellationToken).ConfigureAwait(false);
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        var source = form.Attribution;
        var attribution = Attribution.Create(
            source is { AnalyticsConsent: true } ? ToDomain(source.FirstTouch) : null,
            ToDomain(source?.LastTouch),
            source?.SubmissionPath,
            form.LanguageCode,
            source?.Market,
            source?.DeviceClass,
            source?.AnalyticsClientId,
            source?.AnalyticsConsent ?? false);

        return new PreparedInquiry(
            false, type, form.LanguageCode, contact.Value, form.Message, answers.Value, form.ServiceId, form.ProjectId, form.CallToActionId,
            context.Value, attribution, form.MarketingConsent);
    }

    /// <summary>Referenced items must be live, public content of the right kind. Returns the primary pillar, when known.</summary>
    private async Task<Result<Guid?>> ResolveContextAsync(InquiryType type, InquiryFormDto form, CancellationToken cancellationToken)
    {
        Guid? pillarId = null;
        if (form.ServiceId is { } serviceId)
        {
            var service = await content.GetAsync(serviceId, cancellationToken).ConfigureAwait(false);
            if (service is not { IsLive: true, Type: ContentType.Service or ContentType.ServicePillar })
            {
                return InvalidContext;
            }

            pillarId = service.Type == ContentType.ServicePillar ? service.Id : service.ParentId;
        }

        if (form.ProjectId is { } projectId
            && await content.GetAsync(projectId, cancellationToken).ConfigureAwait(false) is not { IsLive: true, Type: ContentType.Project or ContentType.CaseStudy })
        {
            return InvalidContext;
        }

        if (form.CallToActionId is { } ctaId && await ctas.GetAsync(ctaId, cancellationToken).ConfigureAwait(false) is not { IsActive: true })
        {
            return InvalidContext;
        }

        if (type == InquiryType.ContextualCta && form.CallToActionId is null)
        {
            return Error.Validation("Inquiry.MissingContext", "A call-to-action inquiry needs the call to action it came from.");
        }

        return pillarId;
    }

    private static TouchPoint? ToDomain(TouchPointDto? touch) =>
        touch is null
            ? null
            : new TouchPoint(touch.LandingPath, touch.Referrer, touch.UtmSource, touch.UtmMedium, touch.UtmCampaign, touch.UtmContent, touch.UtmTerm, touch.AtUtc);
}
