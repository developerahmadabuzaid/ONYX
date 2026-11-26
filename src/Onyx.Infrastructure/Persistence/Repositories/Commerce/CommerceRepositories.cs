using Microsoft.EntityFrameworkCore;
using Onyx.Application.Billing;
using Onyx.Application.Customers;
using Onyx.Application.Products;
using Onyx.Application.Subscriptions;
using Onyx.Domain.Billing;
using Onyx.Domain.Customers;
using Onyx.Domain.Products;
using Onyx.Domain.Subscriptions;
using Onyx.Infrastructure.Persistence.Configurations.Application;

namespace Onyx.Infrastructure.Persistence.Repositories;

internal sealed class CustomerAccountRepository(ApplicationDbContext dbContext) : ICustomerAccountRepository
{
    public Task<CustomerAccount?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CustomerAccounts.SingleOrDefaultAsync(account => account.Id == id, cancellationToken);

    public void SetExpectedVersion(CustomerAccount account, uint version) =>
        dbContext.Entry(account).Property(entry => entry.Version).OriginalValue = version;
}

internal sealed class SaasProductRepository(ApplicationDbContext dbContext) : ISaasProductRepository
{
    public Task<SaasProduct?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.SaasProducts.Include(product => product.Translations).Include(product => product.Plans).ThenInclude(plan => plan.Translations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

    public Task<SaasProduct?> GetPublishedBySlugAsync(string language, string slug, CancellationToken cancellationToken)
    {
        var normalized = Domain.Content.Slug.Normalize(slug);
        return dbContext.SaasProducts.Include(product => product.Translations).Include(product => product.Plans).ThenInclude(plan => plan.Translations)
            .AsSplitQuery()
            .Where(product => product.Status == ProductStatus.Published
                && product.Translations.Any(translation => translation.Language == language && translation.Slug == normalized))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken) =>
        dbContext.SaasProducts.AnyAsync(product => product.Key == key, cancellationToken);

    public void Add(SaasProduct product) => dbContext.SaasProducts.Add(product);

    public void SetExpectedVersion(SaasProduct product, uint version) =>
        dbContext.Entry(product).Property(entry => entry.Version).OriginalValue = version;
}

internal sealed class CustomerSubscriptionRepository(ApplicationDbContext dbContext) : ICustomerSubscriptionRepository
{
    private static readonly SubscriptionStatus[] OpenStatuses = [SubscriptionStatus.Requested, SubscriptionStatus.Trial, SubscriptionStatus.Active];

    public Task<CustomerSubscription?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.CustomerSubscriptions.SingleOrDefaultAsync(subscription => subscription.Id == id, cancellationToken);

    public Task<bool> HasOpenAsync(Guid customerId, Guid productId, CancellationToken cancellationToken) =>
        dbContext.CustomerSubscriptions.AnyAsync(
            subscription => subscription.CustomerId == customerId && subscription.ProductId == productId && OpenStatuses.Contains(subscription.Status),
            cancellationToken);

    public Task<bool> HadTrialAsync(Guid customerId, Guid productId, CancellationToken cancellationToken) =>
        dbContext.CustomerSubscriptions.AnyAsync(
            subscription => subscription.CustomerId == customerId && subscription.ProductId == productId && subscription.IsTrial, cancellationToken);

    public async Task<IReadOnlyList<CustomerSubscription>> OpenForCustomerAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.CustomerSubscriptions
            .Where(subscription => subscription.CustomerId == customerId && OpenStatuses.Contains(subscription.Status))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<CustomerSubscription>> TrialsEndedAsync(DateTimeOffset nowUtc, int max, CancellationToken cancellationToken) =>
        await dbContext.CustomerSubscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Trial && subscription.TrialEndsAtUtc <= nowUtc)
            .OrderBy(subscription => subscription.TrialEndsAtUtc)
            .Take(max)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(CustomerSubscription subscription) => dbContext.CustomerSubscriptions.Add(subscription);

    public void SetExpectedVersion(CustomerSubscription subscription, uint version) =>
        dbContext.Entry(subscription).Property(entry => entry.Version).OriginalValue = version;
}

internal sealed class InvoiceRepository(ApplicationDbContext dbContext) : IInvoiceRepository
{
    public Task<Invoice?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Invoices.Include(invoice => invoice.Lines).SingleOrDefaultAsync(invoice => invoice.Id == id, cancellationToken);

    public async Task<string> NextNumberAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var next = await dbContext.Database
            .SqlQueryRaw<long>($"SELECT nextval('{InvoiceConfiguration.NumberSequence}') AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return FormattableString.Invariant($"INV-{nowUtc.UtcDateTime.Year}-{next:D5}");
    }

    public void Add(Invoice invoice) => dbContext.Invoices.Add(invoice);

    public void SetExpectedVersion(Invoice invoice, uint version) =>
        dbContext.Entry(invoice).Property(entry => entry.Version).OriginalValue = version;
}

internal sealed class AiDemoRunRepository(ApplicationDbContext dbContext, TimeProvider timeProvider) : IAiDemoRunRepository
{
    /// <summary>Runs left "Running" this long (a crashed Worker) are claimed again.</summary>
    private static readonly TimeSpan StaleRun = TimeSpan.FromMinutes(10);

    public void Add(AiDemoRun run) => dbContext.AiDemoRuns.Add(run);

    public async Task<IReadOnlyList<AiDemoRun>> ClaimQueuedAsync(int max, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var stale = now.Subtract(StaleRun);
        return await dbContext.AiDemoRuns
            .FromSql($"""
                UPDATE ai_demo_runs
                SET status = 'Running', started_at_utc = {now}
                WHERE id IN (
                    SELECT id FROM ai_demo_runs
                    WHERE status = 'Queued' OR (status = 'Running' AND started_at_utc < {stale})
                    ORDER BY created_at_utc
                    LIMIT {max}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
