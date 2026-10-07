using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Leads;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Consent;
using Onyx.Web.Leads;
using Onyx.Web.Localization;
using Onyx.Web.Resources;
using Onyx.Web.Security;

namespace Onyx.Web.Controllers;

/// <summary>
/// Consultation booking and the visitor manage page. When online booking is not available (switched off or not configured),
/// the same page offers a consultation request instead. The manage page is reached by a signed link: no referrer, no caching,
/// never indexed, and the token is sent to the API only in request bodies.
/// </summary>
public sealed class ConsultationController(
    ILeadsApiClient api,
    LocalizedRouteTable routes,
    LanguageCatalog languages,
    SeoContext seo,
    ConsentState consent,
    IOptions<TurnstileOptions> turnstile,
    IStringLocalizer<SharedResource> localizer,
    TimeProvider timeProvider) : LeadPageController(routes, languages, seo, consent, turnstile, localizer, timeProvider)
{
    public const string BookedSlug = "booked";

    [HttpGet]
    public async Task<IActionResult> Book(string lang, string? slug, [FromQuery] Guid? service, [FromQuery] Guid? cta, CancellationToken cancellationToken)
    {
        if (slug == BookedSlug)
        {
            Describe(LeadPages.BookConsultation, lang, "Booking.Booked.Title", noIndex: true, BookedSlug);
            ViewData[Analytics.AnalyticsTag.ConversionKey] = TempData[Analytics.AnalyticsTag.ConversionKey] as string;
            return View("Booked", TempData[ReferenceKey] as string);
        }

        if (slug is not null)
        {
            return NotFound();
        }

        Describe(LeadPages.BookConsultation, lang, "Booking.Title", noIndex: false);
        var form = new BookingFormModel { ServiceId = service, CtaId = cta, Context = CapturePageContext() };
        return await ShowAsync(form, cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Book(string lang, string? slug, BookingFormModel form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        Describe(LeadPages.BookConsultation, lang, "Booking.Title", noIndex: false);
        if (slug is not null)
        {
            return NotFound();
        }

        if (form.RequestOnly && string.IsNullOrWhiteSpace(form.Message))
        {
            ModelState.AddModelError(nameof(form.Message), Text["Form.Error.MessageRequired"].Value);
        }

        if (!form.RequestOnly && form.StartUtc is null)
        {
            ModelState.AddModelError(nameof(form.StartUtc), Text["Booking.SlotRequired"].Value);
        }

        if (!ModelState.IsValid)
        {
            return await ShowAsync(form, cancellationToken);
        }

        var dto = LeadFormMapper.ToDto(form, lang, 1, form.Answers(), Request, Consent);
        string? reference;
        if (form.RequestOnly)
        {
            var requested = await api.SubmitInquiryAsync(new SubmitInquiryRequest(InquiryTypes.RequestConsultation, dto), cancellationToken);
            if (!requested.IsSuccess)
            {
                AddApiError(requested);
                return await ShowAsync(form, cancellationToken);
            }

            reference = requested.Value!.Reference;
        }
        else
        {
            var booked = await api.BookAsync(new BookConsultationRequest(form.StartUtc!.Value, dto), cancellationToken);
            if (!booked.IsSuccess)
            {
                AddApiError(booked);
                return await ShowAsync(form, cancellationToken);
            }

            reference = booked.Value!.Reference;
        }

        TempData[ReferenceKey] = reference;
        TempData[Analytics.AnalyticsTag.ConversionKey] = form.RequestOnly
            ? Analytics.ConversionEvent.ForInquiry(InquiryTypes.RequestConsultation)
            : Analytics.ConversionEvent.ForBooking();
        return LocalRedirect(PathOf(LeadPages.BookConsultation, lang, BookedSlug)!);
    }

    [HttpGet]
    [SensitivePage]
    [Analytics.NoAnalytics]
    public async Task<IActionResult> Manage(string lang, string? slug, [FromQuery] string? token, CancellationToken cancellationToken)
    {
        Describe(LeadPages.ManageBooking, lang, "Booking.Manage.Title", noIndex: true);
        if (slug is not null || string.IsNullOrWhiteSpace(token))
        {
            return NotFound();
        }

        return await ShowManagedAsync(token, null, cancellationToken);
    }

    /// <summary><paramref name="operation"/> is <c>cancel</c>, <c>reschedule</c> or <c>calendar</c>.</summary>
    [HttpPost]
    [SensitivePage]
    [Analytics.NoAnalytics]
    public async Task<IActionResult> Manage(string lang, string? slug, string? token, string? operation, DateTimeOffset? startUtc, CancellationToken cancellationToken)
    {
        Describe(LeadPages.ManageBooking, lang, "Booking.Manage.Title", noIndex: true);
        if (slug is not null || string.IsNullOrWhiteSpace(token))
        {
            return NotFound();
        }

        switch (operation)
        {
            case "calendar":
                var file = await api.GetCalendarFileAsync(token, cancellationToken);
                if (file.IsSuccess)
                {
                    return File(Encoding.UTF8.GetBytes(file.Value!.Content), "text/calendar; charset=utf-8", file.Value.FileName);
                }

                AddApiError(file);
                break;
            case "cancel":
                var cancelled = await api.CancelBookingAsync(token, cancellationToken);
                if (cancelled.IsSuccess)
                {
                    return ShowManaged(token, cancelled.Value!, null, "Booking.Manage.Cancelled");
                }

                AddApiError(cancelled);
                break;
            case "reschedule" when startUtc is { } start:
                var moved = await api.RescheduleBookingAsync(token, start, cancellationToken);
                if (moved.IsSuccess)
                {
                    return await ShowManagedAsync(token, "Booking.Manage.Rescheduled", cancellationToken);
                }

                AddApiError(moved);
                break;
            default:
                ModelState.AddModelError(string.Empty, Text["Booking.SlotRequired"].Value);
                break;
        }

        return await ShowManagedAsync(token, null, cancellationToken);
    }

    private async Task<IActionResult> ShowAsync(BookingFormModel form, CancellationToken cancellationToken)
    {
        var availability = await api.GetAvailabilityAsync(cancellationToken);
        if (availability.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        var bookable = availability.IsSuccess && availability.Value is { Bookable: true, Slots.Count: > 0 };
        form.RequestOnly = form.RequestOnly || !bookable;
        return View("Book", new BookingPageModel(form, bookable ? availability.Value : null));
    }

    private async Task<IActionResult> ShowManagedAsync(string token, string? notice, CancellationToken cancellationToken)
    {
        var booking = await api.GetManagedBookingAsync(token, cancellationToken);
        if (booking.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (!booking.IsSuccess)
        {
            return View("Manage", new ManageBookingPageModel(token, null, null, notice ?? "Booking.Error.LinkInvalid"));
        }

        AvailabilityResponse? slots = null;
        if (booking.Value!.CanManage)
        {
            var availability = await api.GetAvailabilityAsync(cancellationToken);
            slots = availability.IsSuccess ? availability.Value : null;
        }

        return ShowManaged(token, booking.Value, slots, notice);
    }

    private ViewResult ShowManaged(string token, ManagedBookingResponse booking, AvailabilityResponse? slots, string? notice) =>
        View("Manage", new ManageBookingPageModel(token, booking, slots, notice));
}
