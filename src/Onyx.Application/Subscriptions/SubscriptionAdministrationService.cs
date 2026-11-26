using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Commerce;
using Onyx.Application.Customers;
using Onyx.Application.Email;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Customers;
using Onyx.Domain.Subscriptions;

namespace Onyx.Application.Subscriptions;

/// <summary>
/// Staff decisions on subscriptions (activate, reject, cancel) and the Worker's trial expiry. The customer is emailed in their
/// preferred language through the outbox; the decision is stored whether or not the email is sent.
/// </summary>
public sealed class SubscriptionAdministrationService(
    ICustomerSubscriptionRepository subscriptions,
    ICustomerAccountRepository accounts,
    ICommerceReadModel readModel,
    IEmailOutbox emailOutbox,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public const int ExpiryBatchSize = 100;

    public Task<PagedResult<AdminSubscriptionResponse>> ListAsync(string? status, string language, int page, int pageSize, CancellationToken cancellationToken) =>
        readModel.AdminSubscriptionsAsync(status, CommerceErrors.Language(language), Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public async Task<Result<AdminSubscriptionResponse>> GetAsync(Guid id, string language, CancellationToken cancellationToken) =>
        await readModel.AdminSubscriptionAsync(id, CommerceErrors.Language(language), cancellationToken).ConfigureAwait(false) is { } subscription
            ? subscription
            : CommerceErrors.SubscriptionNotFound;

    public async Task<Result<AdminSubscriptionResponse>> DecideAsync(Guid id, SubscriptionDecisionRequest request, string language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var subscription = await subscriptions.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return CommerceErrors.SubscriptionNotFound;
        }

        subscriptions.SetExpectedVersion(subscription, request.Version);
        var now = timeProvider.GetUtcNow();
        var decided = request.Decision switch
        {
            "activate" => subscription.Activate(request.EndsOn, request.Note, now),
            "reject" => subscription.Reject(request.Note, now),
            "cancel" => subscription.Cancel(now),
            _ => Error.Validation("Subscription.InvalidDecision", "Choose activate, reject or cancel."),
        };
        if (decided.IsFailure)
        {
            return decided.Error!;
        }

        auditTrail.Record(AuditAction.SubscriptionDecided, AuditCategory.Commerce, nameof(CustomerSubscription), id.ToString(),
            new { decision = request.Decision, status = subscription.Status.ToString() });
        await QueueUpdateEmailAsync(subscription, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, language, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Worker: expires trials past their end date (idempotent). Returns the number expired.</summary>
    public async Task<int> ExpireEndedTrialsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ended = await subscriptions.TrialsEndedAsync(now, ExpiryBatchSize, cancellationToken).ConfigureAwait(false);
        var expired = 0;
        foreach (var trial in ended)
        {
            if (trial.ExpireTrial(now))
            {
                expired++;
                await QueueUpdateEmailAsync(trial, cancellationToken).ConfigureAwait(false);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return expired;
    }

    private async Task QueueUpdateEmailAsync(CustomerSubscription subscription, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(subscription.CustomerId, cancellationToken).ConfigureAwait(false);
        if (account is null || account.Status == CustomerAccountStatus.Erased)
        {
            return;
        }

        var product = (await readModel.CustomerSubscriptionsAsync(account.Id, account.PreferredLanguage, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item => item.Id == subscription.Id)?.ProductName;
        await emailOutbox.QueueAsync(
            new EmailRequest(
                CustomerEmailTemplates.SubscriptionUpdated,
                account.PreferredLanguage,
                account.Email,
                account.DisplayName,
                new Dictionary<string, string>
                {
                    ["name"] = account.DisplayName,
                    ["product"] = product ?? string.Empty,
                    ["status"] = subscription.Status.ToString(),
                }),
            cancellationToken).ConfigureAwait(false);
    }
}
