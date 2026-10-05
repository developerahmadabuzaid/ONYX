using Onyx.Application.Abstractions.Messaging;
using Onyx.Application.Email;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Domain.Leads;
using Onyx.Domain.Notifications;

namespace Onyx.Application.Leads;

/// <summary>
/// Worker side of a submission (architecture.md §13.3): the visitor confirmation in the submission language, the internal
/// emails and the NewLead admin notification. Idempotent: the processed-event marker commits with the queued emails and the
/// notification, so a redelivered event does nothing. Booked consultations are announced by the booking handlers instead.
/// </summary>
public sealed class InquirySubmittedHandler(
    IInquiryRepository inquiries,
    IProcessedEventLog processed,
    IEmailOutbox emailOutbox,
    LeadNotifier notifier,
    INotificationService notifications) : IDomainEventHandler<InquirySubmitted>
{
    public async Task HandleAsync(InquirySubmitted domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var inquiry = await inquiries.GetAsync(domainEvent.InquiryId, cancellationToken).ConfigureAwait(false);
        if (inquiry is null || inquiry.Type == InquiryType.Consultation
            || !await processed.TryBeginAsync($"inquiry-submitted:{inquiry.Id:N}", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var reference = InquiryRecorder.Reference(inquiry.Id);
        var confirmation = await emailOutbox.QueueAsync(
            new EmailRequest(
                LeadNotifier.InquiryReceivedTemplate,
                inquiry.LanguageCode,
                inquiry.Email,
                inquiry.Name,
                new Dictionary<string, string> { ["name"] = inquiry.Name, ["reference"] = reference }),
            cancellationToken).ConfigureAwait(false);
        if (confirmation.IsFailure)
        {
            throw new InvalidOperationException($"Visitor confirmation could not be queued: {confirmation.Error!.Code}.");
        }

        await notifier.QueueInternalAsync(
            inquiry.Type,
            LeadNotifier.InquiryInternalTemplate,
            inquiry.LeadId,
            new Dictionary<string, string> { ["type"] = inquiry.Type.ToString(), ["reference"] = reference, ["language"] = inquiry.LanguageCode },
            cancellationToken).ConfigureAwait(false);

        // Publishing saves the unit of work: marker, emails and notification commit together.
        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.NewLead,
                NotificationSeverity.Info,
                Permissions.Leads.View,
                new Dictionary<string, string> { ["type"] = inquiry.Type.ToString() },
                ResourceType: "Lead",
                ResourceId: inquiry.LeadId.ToString()),
            cancellationToken).ConfigureAwait(false);
    }
}
