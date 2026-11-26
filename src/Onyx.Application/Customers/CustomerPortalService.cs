using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Commerce;
using Onyx.Application.Notifications;
using Onyx.Application.Products;
using Onyx.Application.Subscriptions;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Customers;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;
using Onyx.Domain.Subscriptions;

namespace Onyx.Application.Customers;

/// <summary>
/// The signed-in customer's own data: profile, subscriptions and trials, invoices and usage. Every query is scoped to the
/// current customer; another customer's records answer NotFound.
/// </summary>
public sealed class CustomerPortalService(
    ICurrentCustomer current,
    ICommerceReadModel readModel,
    ICustomerAccountRepository accounts,
    ISaasProductRepository products,
    ICustomerSubscriptionRepository subscriptions,
    INotificationService notifications,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    AiDemoOptions aiDemo,
    TimeProvider timeProvider)
{
    public const int RecentInvoices = 5;

    public async Task<Result<CustomerOverviewResponse>> OverviewAsync(string language, CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return CommerceErrors.NotSignedIn;
        }

        var profile = await readModel.CustomerProfileAsync(customerId, cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return CommerceErrors.NotSignedIn;
        }

        var lang = CommerceErrors.Language(language);
        var subscriptionList = await readModel.CustomerSubscriptionsAsync(customerId, lang, cancellationToken).ConfigureAwait(false);
        var invoices = await readModel.CustomerInvoicesAsync(customerId, RecentInvoices, cancellationToken).ConfigureAwait(false);
        var usage = await UsageAsync(customerId, cancellationToken).ConfigureAwait(false);
        return new CustomerOverviewResponse(profile, subscriptionList, invoices, usage);
    }

    public async Task<Result<CustomerProfileResponse>> ProfileAsync(CancellationToken cancellationToken) =>
        current.CustomerId is { } customerId && await readModel.CustomerProfileAsync(customerId, cancellationToken).ConfigureAwait(false) is { } profile
            ? profile
            : CommerceErrors.NotSignedIn;

    public async Task<Result<CustomerProfileResponse>> UpdateProfileAsync(UpdateCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (current.CustomerId is not { } customerId || await accounts.GetAsync(customerId, cancellationToken).ConfigureAwait(false) is not { CanSignIn: true } account)
        {
            return CommerceErrors.NotSignedIn;
        }

        accounts.SetExpectedVersion(account, request.Version);
        var updated = account.UpdateProfile(request.DisplayName, request.Company, request.Language);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        account.SetMarketingConsent(request.MarketingConsent, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await ProfileAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<CustomerSubscriptionResponse>>> SubscriptionsAsync(string language, CancellationToken cancellationToken) =>
        current.CustomerId is { } customerId
            ? Result.Success(await readModel.CustomerSubscriptionsAsync(customerId, CommerceErrors.Language(language), cancellationToken).ConfigureAwait(false))
            : CommerceErrors.NotSignedIn;

    /// <summary>
    /// Starts a trial immediately when the product offers one and the customer never had one; otherwise records a request that
    /// staff activate or reject. One open subscription per product.
    /// </summary>
    public async Task<Result<CustomerSubscriptionResponse>> StartSubscriptionAsync(StartSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (current.CustomerId is not { } customerId || await accounts.GetAsync(customerId, cancellationToken).ConfigureAwait(false) is not { CanSignIn: true })
        {
            return CommerceErrors.NotSignedIn;
        }

        if (request.Intent is not (CustomerIntents.Trial or CustomerIntents.Subscribe))
        {
            return Error.Validation("Subscription.InvalidIntent", "Choose a trial or a subscription.");
        }

        var lang = CommerceErrors.Language(request.Language);
        var product = await products.GetPublishedBySlugAsync(lang, request.ProductSlug ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return CommerceErrors.ProductNotFound;
        }

        if (request.PlanId is { } planId && product.FindPlan(planId) is not { IsActive: true })
        {
            return Error.Validation("Subscription.InvalidPlan", "Choose one of the product's plans.");
        }

        if (await subscriptions.HasOpenAsync(customerId, product.Id, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Subscription.AlreadyOpen", "You already have an open subscription or request for this product.");
        }

        var now = timeProvider.GetUtcNow();
        Result<CustomerSubscription> created;
        if (request.Intent == CustomerIntents.Trial)
        {
            if (!product.OffersTrial)
            {
                return Error.Conflict("Subscription.NoTrial", "This product has no trial.");
            }

            if (await subscriptions.HadTrialAsync(customerId, product.Id, cancellationToken).ConfigureAwait(false))
            {
                return Error.Conflict("Subscription.TrialUsed", "You have already used the trial of this product.");
            }

            created = CustomerSubscription.StartTrial(customerId, product.Id, request.PlanId, product.TrialDays!.Value, now);
        }
        else
        {
            created = CustomerSubscription.Request(customerId, product.Id, request.PlanId, request.Note, now);
        }

        if (created.IsFailure)
        {
            return created.Error!;
        }

        subscriptions.Add(created.Value);
        auditTrail.Record(AuditAction.SubscriptionRequested, AuditCategory.Commerce, nameof(CustomerSubscription), created.Value.Id.ToString(),
            new { product = product.Key, trial = created.Value.IsTrial });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await notifications.PublishAsync(
            new NotificationRequest(
                NotificationType.NewSubscriptionRequest,
                NotificationSeverity.Info,
                Permissions.Subscriptions.View,
                new Dictionary<string, string> { ["product"] = product.Key, ["kind"] = created.Value.IsTrial ? "trial" : "request" },
                nameof(CustomerSubscription),
                created.Value.Id.ToString()),
            cancellationToken).ConfigureAwait(false);

        var list = await readModel.CustomerSubscriptionsAsync(customerId, lang, cancellationToken).ConfigureAwait(false);
        return list.Single(item => item.Id == created.Value.Id);
    }

    public async Task<Result> CancelSubscriptionAsync(Guid subscriptionId, CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (current.CustomerId is not { } customerId)
        {
            return CommerceErrors.NotSignedIn;
        }

        var subscription = await subscriptions.GetAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || subscription.CustomerId != customerId)
        {
            return CommerceErrors.SubscriptionNotFound;
        }

        subscriptions.SetExpectedVersion(subscription, request.Version);
        var cancelled = subscription.Cancel(timeProvider.GetUtcNow());
        if (cancelled.IsFailure)
        {
            return cancelled;
        }

        auditTrail.Record(AuditAction.SubscriptionCancelled, AuditCategory.Commerce, nameof(CustomerSubscription), subscription.Id.ToString(), new { byCustomer = true });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<CustomerInvoiceResponse>>> InvoicesAsync(CancellationToken cancellationToken) =>
        current.CustomerId is { } customerId
            ? Result.Success(await readModel.CustomerInvoicesAsync(customerId, 200, cancellationToken).ConfigureAwait(false))
            : CommerceErrors.NotSignedIn;

    public async Task<Result<CustomerInvoiceResponse>> InvoiceAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        if (current.CustomerId is not { } customerId)
        {
            return CommerceErrors.NotSignedIn;
        }

        return await readModel.CustomerInvoiceAsync(customerId, invoiceId, cancellationToken).ConfigureAwait(false) is { } invoice
            ? invoice
            : CommerceErrors.InvoiceNotFound;
    }

    private async Task<CustomerUsageResponse> UsageAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var month = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var settings = aiDemo;
        return new CustomerUsageResponse(
            settings.Enabled,
            await readModel.AiDemoRunsSinceAsync(customerId, today, cancellationToken).ConfigureAwait(false),
            settings.PerCustomerDailyLimit,
            await readModel.AiDemoRunsSinceAsync(customerId, month, cancellationToken).ConfigureAwait(false));
    }
}
