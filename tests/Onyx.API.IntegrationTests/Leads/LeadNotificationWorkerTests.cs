using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Onyx.API.IntegrationTests.Infrastructure;
using Onyx.API.IntegrationTests.Worker;
using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Email;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Booking;
using Onyx.Contracts.Common;
using Onyx.Contracts.Leads;
using Onyx.Contracts.Notifications;
using Onyx.Domain.Leads;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.BackgroundJobs;
using Onyx.Infrastructure.Email;

namespace Onyx.API.IntegrationTests.Leads;

/// <summary>
/// Worker side of submissions and bookings: emails in the submission language, internal emails without contact details,
/// permission-scoped notifications, idempotent redelivery, and inquiries that survive a failing email transport.
/// </summary>
public sealed class LeadNotificationWorkerTests(PostgresFixture fixture) : LeadsApiTestBase(fixture)
{
    private readonly FakeEmailSender _sender = new();

    [Fact]
    public async Task Submitted_inquiries_queue_both_emails_and_a_permission_scoped_notification_exactly_once()
    {
        Fixture.RequireDatabase();
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact,
            Form(email: "arabic@example.com", language: "ar", message: "نحتاج إلى أتمتة الموافقات الداخلية."))));
        await using var worker = CreateWorker();

        await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        await using (var db = await DbAsync())
        {
            var emails = await db.EmailMessages.OrderBy(message => message.TemplateKey).ToListAsync(Token);
            Assert.Equal(["leads.inquiry-internal", "leads.inquiry-received"], emails.Select(message => message.TemplateKey).ToArray());

            var visitor = emails.Single(message => message.TemplateKey == "leads.inquiry-received");
            Assert.Equal("ar", visitor.LanguageCode);
            Assert.Equal("arabic@example.com", visitor.ToAddress);
            Assert.Contains("dir=\"rtl\"", visitor.HtmlBody, StringComparison.Ordinal);

            var lead = await db.Leads.SingleAsync(Token);
            var internalEmail = emails.Single(message => message.TemplateKey == "leads.inquiry-internal");
            Assert.Equal(Recipient, internalEmail.ToAddress);
            Assert.Contains($"{OnyxApiFactory.AdminPortalBaseUrl}/admin/leads/{lead.Id:D}", internalEmail.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("arabic@example.com", internalEmail.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("أتمتة الموافقات", internalEmail.TextBody, StringComparison.Ordinal);
            Assert.DoesNotContain("Sample Visitor", internalEmail.TextBody, StringComparison.Ordinal);

            var notification = await db.AdminNotifications.SingleAsync(Token);
            Assert.Equal(NotificationType.NewLead, notification.Type);
            Assert.Equal(Permissions.Leads.View, notification.RequiredPermission);
        }

        // Redelivery of the same event (at-least-once outbox) changes nothing.
        var inquiry = await QueryAsync(db => db.Inquiries.Select(item => new { item.Id, item.LeadId }));
        await worker.InScopeAsync(async services =>
        {
            await services.GetRequiredService<IDomainEventHandler<InquirySubmitted>>().HandleAsync(new InquirySubmitted(inquiry[0].Id, inquiry[0].LeadId), Token);
            return true;
        });
        Assert.Equal(2, await CountAsync(db => db.EmailMessages));
        Assert.Equal(1, await CountAsync(db => db.AdminNotifications));

        // Only users who may see leads see the notification.
        var sales = await SignInWithAsync("sales", Permissions.Leads.View);
        var editor = await SignInWithAsync("editor", Permissions.Content.View);
        var salesView = await ReadAsync<PagedResult<AdminNotificationResponse>>(await sales.GetAsync("/api/v1/admin/notifications", Token));
        Assert.Equal("NewLead", Assert.Single(salesView.Items).Type);
        Assert.Empty((await ReadAsync<PagedResult<AdminNotificationResponse>>(await editor.GetAsync("/api/v1/admin/notifications", Token))).Items);
    }

    [Fact]
    public async Task The_inquiry_is_kept_when_the_email_transport_fails()
    {
        Fixture.RequireDatabase();
        Assert.Equal(HttpStatusCode.Created, await StatusAsync(SubmitAsync(InquiryTypes.Contact, Form())));
        _sender.Behaviour = _ => throw new EmailTransportException(EmailFailureKind.MessageRejected, "Rejected by test transport.");
        await using var worker = CreateWorker();

        await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);
        await worker.Get<EmailProcessor>().ProcessBatchAsync(Token);

        await using var db = await DbAsync();
        Assert.Equal(1, await db.Inquiries.CountAsync(Token));
        Assert.Equal(1, await db.Leads.CountAsync(Token));
        Assert.Equal(1, await db.ConsentRecords.CountAsync(Token));
        var emails = await db.EmailMessages.ToListAsync(Token);
        Assert.Equal(2, emails.Count);
        Assert.All(emails, message =>
        {
            Assert.Equal(EmailMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.AttemptCount);
        });
        Assert.Empty(_sender.Sent);
    }

    [Fact]
    public async Task Bookings_notify_staff_through_the_worker_and_a_consultation_is_not_announced_twice()
    {
        Fixture.RequireDatabase();
        var (_, owner) = await SignInSuperAdminAsync();
        await OpenBookingAsync(owner);
        var slot = (await ReadAsync<AvailabilityResponse>(await Client.GetAsync("/api/v1/consultations/availability", Token))).Slots[0];
        await ReadAsync<BookingConfirmedResponse>(await Client.PostAsJsonAsync(
            "/api/v1/consultations", new BookConsultationRequest(slot.StartUtc, Form(email: "booked@example.com", token: Solved("booking"))), Token));
        await using var worker = CreateWorker();

        await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        await using (var db = await DbAsync())
        {
            // Visitor confirmation (queued with the booking) + one internal booking email; no separate inquiry emails.
            var templates = await db.EmailMessages.Select(message => message.TemplateKey).OrderBy(key => key).ToListAsync(Token);
            Assert.Equal(["bookings.confirmed", "bookings.internal"], templates);
            var notification = await db.AdminNotifications.SingleAsync(Token);
            Assert.Equal(NotificationType.NewBooking, notification.Type);
            Assert.Equal(Permissions.Bookings.View, notification.RequiredPermission);
        }

        var booking = (await ReadAsync<PagedResult<BookingResponse>>(await owner.GetAsync("/api/v1/admin/bookings", Token))).Items.Single();
        await ReadAsync<BookingResponse>(await owner.PostAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/cancel", new CancelBookingRequest(false, booking.Version), Token));
        await worker.Get<OutboxProcessor>().ProcessBatchAsync(Token);

        await using (var db = await DbAsync())
        {
            Assert.Contains(NotificationType.BookingCancelled, await db.AdminNotifications.Select(item => item.Type).ToListAsync(Token));
            Assert.Equal(2, await db.EmailMessages.CountAsync(message => message.TemplateKey == "bookings.internal", Token));
            Assert.DoesNotContain(await db.EmailMessages.Select(message => message.TemplateKey).ToListAsync(Token), key => key == "bookings.cancelled");
        }
    }

    private WorkerTestHost CreateWorker() =>
        WorkerTestHost.Create(
            Fixture.ApplicationDatabase!.ConnectionString,
            "worker-leads",
            Time,
            services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(_sender);
            },
            new Dictionary<string, string?>
            {
                ["Leads:DefaultRecipients:0"] = Recipient,
                ["IdentitySecurity:AdminPortalBaseUrl"] = OnyxApiFactory.AdminPortalBaseUrl,
                ["Site:PublicBaseUrl"] = $"https://{SiteHost}",
            });
}
