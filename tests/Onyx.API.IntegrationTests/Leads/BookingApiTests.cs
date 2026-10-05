using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.Contracts.Booking;
using Onyx.Domain.Booking;
using Onyx.Domain.Leads;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>Consultation booking: closed until configured, one transaction, unique slots under concurrency, signed visitor links.</summary>
public sealed partial class BookingApiTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    [Fact]
    public async Task Booking_is_closed_until_an_administrator_configures_it()
    {
        Fixture.RequireDatabase();

        var availability = await ReadAsync<AvailabilityResponse>(await Client.GetAsync("/api/v1/consultations/availability", Token));
        Assert.False(availability.Bookable);
        Assert.Empty(availability.Slots);
        Assert.Equal("Asia/Riyadh", availability.TimeZoneId);

        using var refused = await Client.PostAsJsonAsync("/api/v1/consultations", new BookConsultationRequest(Time.GetUtcNow().AddDays(1), Form(token: Solved("booking"))), Token);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Booking.Closed", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(db => db.ConsultationBookings));
    }

    [Fact]
    public async Task Booking_stores_booking_inquiry_lead_and_the_visitor_confirmation_with_calendar_file_together()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slot = (await AvailabilityAsync()).Slots[0];

        var booked = await BookAsync(slot.StartUtc, "booker@example.com", "ar");
        Assert.Equal(slot.StartUtc, booked.StartUtc);
        Assert.Equal("Asia/Riyadh", booked.TimeZoneId);

        await using var db = await DbAsync();
        var booking = await db.ConsultationBookings.SingleAsync(Token);
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal("ar", booking.LanguageCode);
        Assert.Equal(64, booking.ManageTokenHash!.Length);
        var inquiry = await db.Inquiries.SingleAsync(Token);
        Assert.Equal(InquiryType.Consultation, inquiry.Type);
        Assert.Equal(booking.InquiryId, inquiry.Id);
        Assert.Equal(booking.LeadId, inquiry.LeadId);
        Assert.Contains("booked-consultation", (await db.Leads.SingleAsync(Token)).ScoreReasons);

        var email = await db.EmailMessages.SingleAsync(Token);
        Assert.Equal("bookings.confirmed", email.TemplateKey);
        Assert.Equal("ar", email.LanguageCode);
        var attachment = Assert.Single(email.Attachments!);
        Assert.Equal("text/calendar", attachment.ContentType);
        Assert.Contains("BEGIN:VEVENT", attachment.Content, StringComparison.Ordinal);
        Assert.Contains("https://onyx.test/ar/", email.TextBody, StringComparison.Ordinal);

        // The slot is gone from availability.
        Assert.DoesNotContain((await AvailabilityAsync()).Slots, free => free.StartUtc == slot.StartUtc);
    }

    [Fact]
    public async Task Concurrent_bookings_of_the_same_slot_confirm_exactly_one()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slot = (await AvailabilityAsync()).Slots[0];

        var attempts = Enumerable.Range(0, 4).Select(index =>
            Client.PostAsJsonAsync("/api/v1/consultations", new BookConsultationRequest(slot.StartUtc, Form(email: $"racer{index}@example.com", token: Solved("booking"))), Token));
        var responses = await Task.WhenAll(attempts);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.All(responses.Where(response => response.StatusCode != HttpStatusCode.Created), response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var db = await DbAsync();
        Assert.Equal(1, await db.ConsultationBookings.CountAsync(booking => booking.Status == BookingStatus.Confirmed, Token));

        // The losing requests rolled back completely: one inquiry, one lead, one visitor email.
        Assert.Equal(1, await db.Inquiries.CountAsync(Token));
        Assert.Equal(1, await db.EmailMessages.CountAsync(Token));
    }

    [Fact]
    public async Task Booking_tokens_are_checked_for_the_booking_action()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slot = (await AvailabilityAsync()).Slots[0];

        using var response = await Client.PostAsJsonAsync("/api/v1/consultations", new BookConsultationRequest(slot.StartUtc, Form(token: Solved("inquiry"))), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(db => db.ConsultationBookings));
    }

    [Fact]
    public async Task The_signed_link_reschedules_serves_the_calendar_file_and_cancels()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slots = (await AvailabilityAsync()).Slots;
        await BookAsync(slots[0].StartUtc, "manage@example.com", "en");
        var token = await ManageTokenAsync();

        var managed = await ReadAsync<ManagedBookingResponse>(await Client.PostAsJsonAsync("/api/v1/consultations/manage", new ManageBookingRequest(token), Token));
        Assert.True(managed.CanManage);
        Assert.Equal(slots[0].StartUtc, managed.StartUtc);

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.PostAsJsonAsync("/api/v1/consultations/manage", new ManageBookingRequest(new string('a', 43)), Token)));
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.PostAsJsonAsync("/api/v1/consultations/manage", new ManageBookingRequest("short"), Token)));

        var moved = await ReadAsync<ManagedBookingResponse>(await Client.PostAsJsonAsync(
            "/api/v1/consultations/manage/reschedule", new RescheduleBookingRequest(token, slots[1].StartUtc), Token));
        Assert.Equal(slots[1].StartUtc, moved.StartUtc);

        var calendar = await ReadAsync<CalendarFileResponse>(await Client.PostAsJsonAsync("/api/v1/consultations/manage/calendar", new ManageBookingRequest(token), Token));
        Assert.Contains("STATUS:CONFIRMED", calendar.Content, StringComparison.Ordinal);

        var cancelled = await ReadAsync<ManagedBookingResponse>(await Client.PostAsJsonAsync("/api/v1/consultations/manage/cancel", new ManageBookingRequest(token), Token));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.False(cancelled.CanManage);

        // The link stops working once the booking is no longer active.
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(Client.PostAsJsonAsync("/api/v1/consultations/manage/cancel", new ManageBookingRequest(token), Token)));

        await using var db = await DbAsync();
        var bookings = await db.ConsultationBookings.OrderBy(booking => booking.CreatedAtUtc).ToListAsync(Token);
        Assert.Equal(BookingStatus.Rescheduled, bookings[0].Status);
        Assert.Equal(bookings[1].Id, bookings[0].RescheduledToId);
        Assert.Equal(BookingStatus.Cancelled, bookings[1].Status);
        Assert.True(bookings[1].CancelledByVisitor);
        Assert.All(bookings, booking => Assert.Null(booking.ManageTokenHash));

        // Confirmation, re-confirmation and cancellation emails, all in the booking language.
        var templates = await db.EmailMessages.OrderBy(message => message.CreatedAtUtc).Select(message => message.TemplateKey).ToListAsync(Token);
        Assert.Equal(["bookings.confirmed", "bookings.confirmed", "bookings.cancelled"], templates);
    }

    [Fact]
    public async Task Staff_record_outcomes_only_after_the_slot_has_started()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slot = (await AvailabilityAsync()).Slots[0];
        await BookAsync(slot.StartUtc, "outcome@example.com", "en");
        var booking = (await ReadAsync<Contracts.Common.PagedResult<BookingResponse>>(await owner.GetAsync("/api/v1/admin/bookings", Token))).Items.Single();

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync(
            $"/api/v1/admin/bookings/{booking.Id}/outcome", new RecordBookingOutcomeRequest("Completed", booking.Version), Token)));

        var cancelled = await ReadAsync<BookingResponse>(await owner.PostAsJsonAsync(
            $"/api/v1/admin/bookings/{booking.Id}/cancel", new CancelBookingRequest(true, booking.Version), Token));
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(2, await CountAsync(db => db.EmailMessages));
    }

    [Fact]
    public async Task Availability_rules_cannot_overlap_and_blocked_dates_remove_slots()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);

        Assert.Equal(HttpStatusCode.Conflict, await StatusAsync(owner.PostAsJsonAsync(
            "/api/v1/admin/availability/rules", new SaveAvailabilityRuleRequest("Sunday", "16:00", "18:00"), Token)));
        Assert.Equal(HttpStatusCode.BadRequest, await StatusAsync(owner.PutAsJsonAsync(
            "/api/v1/admin/availability/settings", new SaveBookingSettingsRequest(true, "Mars/Olympus", 30, 0, 0, 7, null), Token)));

        var first = (await AvailabilityAsync()).Slots[0];
        var blocked = DateOnly.FromDateTime(first.StartLocal.DateTime);
        await ReadAsync<BookingSettingsResponse>(await owner.PostAsJsonAsync("/api/v1/admin/availability/exceptions", new SaveAvailabilityExceptionRequest(blocked, "Holiday"), Token));

        Assert.DoesNotContain((await AvailabilityAsync()).Slots, slot => DateOnly.FromDateTime(slot.StartLocal.DateTime) == blocked);
    }

    private async Task<AvailabilityResponse> AvailabilityAsync() =>
        await ReadAsync<AvailabilityResponse>(await Client.GetAsync("/api/v1/consultations/availability", Token));

    private async Task<BookingConfirmedResponse> BookAsync(DateTimeOffset startUtc, string email, string language) =>
        await ReadAsync<BookingConfirmedResponse>(await Client.PostAsJsonAsync(
            "/api/v1/consultations", new BookConsultationRequest(startUtc, Form(email: email, language: language, token: Solved("booking"))), Token));

    /// <summary>The raw manage token exists only in the visitor email (the database keeps its hash).</summary>
    private async Task<string> ManageTokenAsync()
    {
        await using var db = await DbAsync();
        var body = await db.EmailMessages.Where(message => message.TemplateKey == "bookings.confirmed").Select(message => message.TextBody).FirstAsync(Token);
        return ManageTokenPattern().Match(body).Groups[1].Value;
    }

    [GeneratedRegex("token=([A-Za-z0-9_-]{43})")]
    private static partial Regex ManageTokenPattern();
}
