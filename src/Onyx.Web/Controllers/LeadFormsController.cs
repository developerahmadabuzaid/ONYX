using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Leads;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Consent;
using Onyx.Web.Leads;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

/// <summary>
/// The project brief wizard and the contact form. The contact form becomes a service inquiry, a similar-project inquiry or a
/// call-to-action inquiry when opened from a service, a project or a call to action. Submissions go to the API, which stores
/// them before any email is sent; a successful post redirects to a confirmation page (no resubmission on refresh).
/// </summary>
public sealed class LeadFormsController(
    ILeadsApiClient api,
    IPublicContentApiClient content,
    LocalizedRouteTable routes,
    LanguageCatalog languages,
    SeoContext seo,
    ConsentState consent,
    IOptions<TurnstileOptions> turnstile,
    IStringLocalizer<SharedResource> localizer,
    TimeProvider timeProvider) : LeadPageController(routes, languages, seo, consent, turnstile, localizer, timeProvider)
{
    public const string SentSlug = "sent";

    [HttpGet]
    public IActionResult StartProject(string lang, string? slug, [FromQuery] Guid? service, [FromQuery] Guid? cta, [FromQuery] string? pillar)
    {
        if (slug == SentSlug)
        {
            return Sent(LeadPages.StartProject, lang);
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(LeadPages.StartProject, lang, "Lead.StartProject.Title", noIndex: false);
        // A service page preselects its pillar (?pillar=ai); unknown values are ignored.
        var preselected = pillar is not null && StartProjectFormModel.PillarOptions.Contains(pillar) ? new List<string> { pillar } : [];
        return View(new StartProjectFormModel { ServiceId = service, CtaId = cta, Pillars = preselected, Context = CapturePageContext() });
    }

    [HttpPost]
    public async Task<IActionResult> StartProject(string lang, string? slug, StartProjectFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        Describe(LeadPages.StartProject, lang, "Lead.StartProject.Title", noIndex: false);
        if (slug is not null)
        {
            return NotFound();
        }

        // Contact fields are validated only on the last step; earlier steps check their own answers.
        if (form.Command == "back")
        {
            ModelState.Clear();
            form.Step = Math.Max(1, form.Step - 1);
            return View(form);
        }

        if (form.Step < StartProjectFormModel.LastStep)
        {
            ModelState.Clear();
            if (form.Step == 1 && !form.Pillars.Any(StartProjectFormModel.PillarOptions.Contains))
            {
                ModelState.AddModelError(nameof(form.Pillars), Text["Lead.StartProject.PillarRequired"].Value);
                return View(form);
            }

            if (form.Step == 2 && string.IsNullOrWhiteSpace(form.Message))
            {
                ModelState.AddModelError(nameof(form.Message), Text["Form.Error.MessageRequired"].Value);
                return View(form);
            }

            form.Step++;
            return View(form);
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var request = new SubmitInquiryRequest(
            InquiryTypes.StartProject, LeadFormMapper.ToDto(form, lang, StartProjectFormModel.AnswersVersion, form.Answers(), Request, Consent));
        var result = await api.SubmitInquiryAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result);
            return View(form);
        }

        TempData[ReferenceKey] = result.Value!.Reference;
        TempData[Analytics.AnalyticsTag.ConversionKey] = Analytics.ConversionEvent.ForInquiry(InquiryTypes.StartProject);
        return LocalRedirect(PathOf(LeadPages.StartProject, lang, SentSlug)!);
    }

    [HttpGet]
    public async Task<IActionResult> Contact(string lang, string? slug, [FromQuery] Guid? service, [FromQuery] Guid? project, [FromQuery] Guid? cta, CancellationToken cancellationToken)
    {
        if (slug == SentSlug)
        {
            return Sent(LeadPages.Contact, lang);
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(LeadPages.Contact, lang, "Lead.Contact.Title", noIndex: service is not null || project is not null || cta is not null);
        await DescribeContactDetailsAsync(cancellationToken);
        return View(new InquiryFormModel { ServiceId = service, ProjectId = project, CtaId = cta, Context = CapturePageContext() });
    }

    [HttpPost]
    public async Task<IActionResult> Contact(string lang, string? slug, InquiryFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        Describe(LeadPages.Contact, lang, "Lead.Contact.Title", noIndex: true);
        await DescribeContactDetailsAsync(cancellationToken);
        if (slug is not null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(form.Message))
        {
            ModelState.AddModelError(nameof(form.Message), Text["Form.Error.MessageRequired"].Value);
        }

        if (!ModelState.IsValid)
        {
            return View(form);
        }

        var type = form.ServiceId is not null ? InquiryTypes.ServiceInquiry
            : form.ProjectId is not null ? InquiryTypes.SimilarProject
            : form.CtaId is not null ? InquiryTypes.ContextualCta
            : InquiryTypes.Contact;
        var result = await api.SubmitInquiryAsync(new SubmitInquiryRequest(type, LeadFormMapper.ToDto(form, lang, 1, null, Request, Consent)), cancellationToken);
        if (!result.IsSuccess)
        {
            AddApiError(result);
            return View(form);
        }

        TempData[ReferenceKey] = result.Value!.Reference;
        TempData[Analytics.AnalyticsTag.ConversionKey] = Analytics.ConversionEvent.ForInquiry(type);
        return LocalRedirect(PathOf(LeadPages.Contact, lang, SentSlug)!);
    }

    private ViewResult Sent(string pageKey, string lang)
    {
        Describe(pageKey, lang, "Lead.Sent.Title", noIndex: true, SentSlug);
        // The conversion event travels once from the successful POST: a refresh of this page never sends it again.
        ViewData[Analytics.AnalyticsTag.ConversionKey] = TempData[Analytics.AnalyticsTag.ConversionKey] as string;
        return View("Sent", TempData[ReferenceKey] as string);
    }

    /// <summary>Contact details shown beside the form: only owner-verified facts from the API (organization email), never guessed.</summary>
    private async Task DescribeContactDetailsAsync(CancellationToken cancellationToken)
    {
        ViewData["Nav"] = "contact";
        var organization = await content.GetOrganizationAsync(cancellationToken);
        ViewData["VerifiedEmail"] = string.IsNullOrWhiteSpace(organization?.Email) ? null : organization.Email;
    }
}
