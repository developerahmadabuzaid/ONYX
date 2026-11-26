using Microsoft.EntityFrameworkCore;
using Onyx.Application.Commerce;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;
using Onyx.Domain.Billing;
using Onyx.Domain.Customers;
using Onyx.Domain.Products;
using Onyx.Domain.Subscriptions;

namespace Onyx.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only projections for the catalogue, the customer portal and the Admin commerce screens. Customer queries always filter
/// by the customer id; drafts are never returned to customers. The catalogue is small, so products load with their children.
/// </summary>
internal sealed class CommerceReadModel(ApplicationDbContext dbContext) : ICommerceReadModel
{
    public async Task<IReadOnlyList<PublicProductListItemResponse>> PublicProductsAsync(string language, CancellationToken cancellationToken)
    {
        var products = await PublishedProducts().ToListAsync(cancellationToken).ConfigureAwait(false);
        return products
            .Where(product => product.Translation(language) is not null)
            .Select(product =>
            {
                var copy = product.Translation(language)!;
                return new PublicProductListItemResponse(
                    product.Id, copy.Name, copy.Slug, copy.Summary, product.TrialDays, product.DemoKind.ToString(),
                    product.Plans.Any(plan => plan.IsActive && plan.ShowsPrice), Slugs(product));
            })
            .ToList();
    }

    public async Task<PublicProductResponse?> PublicProductAsync(string language, string slug, CancellationToken cancellationToken)
    {
        var normalized = Domain.Content.Slug.Normalize(slug);
        var product = await PublishedProducts()
            .Where(candidate => candidate.Translations.Any(translation => translation.Language == language && translation.Slug == normalized))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (product?.Translation(language) is not { } copy)
        {
            return null;
        }

        var plans = product.Plans
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.SortOrder)
            .Select(plan =>
            {
                var text = plan.Translations.SingleOrDefault(translation => translation.Language == language);
                return new PublicPlanResponse(
                    plan.Id, plan.Code, text?.Name ?? plan.Code, text?.Description,
                    plan.ShowsPrice ? plan.Price : null, plan.ShowsPrice ? plan.Currency : null, plan.BillingPeriod.ToString());
            })
            .ToList();
        return new PublicProductResponse(product.Id, copy.Name, copy.Slug, copy.Summary, copy.Description, product.TrialDays,
            product.DemoKind.ToString(), product.DemoUrl, plans, Slugs(product));
    }

    public async Task<IReadOnlyList<AdminProductListItemResponse>> AdminProductsAsync(string language, CancellationToken cancellationToken)
    {
        var products = await dbContext.SaasProducts.AsNoTracking().Include(product => product.Translations).Include(product => product.Plans)
            .AsSplitQuery()
            .OrderBy(product => product.SortOrder).ThenBy(product => product.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return products.Select(product => new AdminProductListItemResponse(
                product.Id, product.Key, product.Translation(language)?.Name ?? product.Key, product.Status.ToString(), product.IsVerified,
                product.Plans.Count, product.SortOrder, product.UpdatedAtUtc ?? product.CreatedAtUtc))
            .ToList();
    }

    public async Task<AdminProductResponse?> AdminProductAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await dbContext.SaasProducts.AsNoTracking()
            .Include(candidate => candidate.Translations)
            .Include(candidate => candidate.Plans).ThenInclude(plan => plan.Translations)
            .AsSplitQuery()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
        {
            return null;
        }

        var verifiedBy = product.VerifiedById is { } userId
            ? await dbContext.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => user.DisplayName).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;
        return new AdminProductResponse(
            product.Id, product.Key, product.Status.ToString(), product.TrialDays, product.DemoKind.ToString(), product.DemoUrl, product.SortOrder,
            product.VerificationSource, product.VerifiedAtUtc, verifiedBy, product.PublishedAtUtc,
            product.Translations.OrderBy(translation => translation.Language, StringComparer.Ordinal)
                .Select(translation => new ProductTranslationDto(translation.Language, translation.Name, translation.Slug, translation.Summary, translation.Description))
                .ToList(),
            product.Plans.OrderBy(plan => plan.SortOrder).ThenBy(plan => plan.Code, StringComparer.Ordinal)
                .Select(plan => new AdminPlanResponse(
                    plan.Id, plan.Code, plan.Price, plan.Currency, plan.BillingPeriod.ToString(), plan.IsPricePublic, plan.IsActive, plan.SortOrder,
                    plan.Translations.OrderBy(translation => translation.Language, StringComparer.Ordinal)
                        .Select(translation => new PlanTranslationDto(translation.Language, translation.Name, translation.Description))
                        .ToList()))
                .ToList(),
            product.Version);
    }

    public Task<bool> ProductSlugTakenAsync(string language, string slug, Guid exceptProductId, CancellationToken cancellationToken) =>
        dbContext.SaasProductTranslations.AnyAsync(
            translation => translation.Language == language && translation.Slug == slug && translation.ProductId != exceptProductId, cancellationToken);

    public Task<CustomerProfileResponse?> CustomerProfileAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.CustomerAccounts.AsNoTracking()
            .Where(account => account.Id == customerId && account.Status == CustomerAccountStatus.Active)
            .Select(account => new CustomerProfileResponse(
                account.Id, account.Email, account.DisplayName, account.Company, account.PreferredLanguage, account.MarketingConsent,
                account.TermsVersion, account.TermsAcceptedAtUtc, account.CreatedAtUtc, account.Version))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CustomerSubscriptionResponse>> CustomerSubscriptionsAsync(Guid customerId, string language, CancellationToken cancellationToken)
    {
        var rows = await SubscriptionRows(language).Where(row => row.Subscription.CustomerId == customerId)
            .OrderByDescending(row => row.Subscription.RequestedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => new CustomerSubscriptionResponse(
                row.Subscription.Id, row.Subscription.ProductId, row.ProductName ?? string.Empty, row.ProductSlug ?? string.Empty, row.PlanName,
                row.Subscription.Status.ToString(), row.Subscription.IsTrial, row.Subscription.RequestedAtUtc, row.Subscription.TrialEndsAtUtc,
                row.Subscription.ActivatedAtUtc, row.Subscription.EndsOn, row.Subscription.Version))
            .ToList();
    }

    public async Task<IReadOnlyList<CustomerInvoiceResponse>> CustomerInvoicesAsync(Guid customerId, int max, CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Invoices.AsNoTracking().Include(invoice => invoice.Lines)
            .Where(invoice => invoice.CustomerId == customerId && invoice.Status != InvoiceStatus.Draft)
            .OrderByDescending(invoice => invoice.IssuedAtUtc)
            .Take(max)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return invoices.Select(ToCustomerInvoice).ToList();
    }

    public async Task<CustomerInvoiceResponse?> CustomerInvoiceAsync(Guid customerId, Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices.AsNoTracking().Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == invoiceId && candidate.CustomerId == customerId && candidate.Status != InvoiceStatus.Draft, cancellationToken)
            .ConfigureAwait(false);
        return invoice is null ? null : ToCustomerInvoice(invoice);
    }

    public async Task<AiDemoRunResponse?> AiDemoRunAsync(Guid customerId, Guid runId, string language, CancellationToken cancellationToken) =>
        (await DemoRuns(language).Where(row => row.Run.CustomerId == customerId && row.Run.Id == runId).ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToDemoRun).SingleOrDefault();

    public async Task<IReadOnlyList<AiDemoRunResponse>> RecentAiDemoRunsAsync(Guid customerId, Guid productId, string language, int max, CancellationToken cancellationToken) =>
        (await DemoRuns(language).Where(row => row.Run.CustomerId == customerId && row.Run.ProductId == productId)
            .OrderByDescending(row => row.Run.CreatedAtUtc).Take(max)
            .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToDemoRun).ToList();

    public Task<int> AiDemoRunsSinceAsync(Guid? customerId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
        dbContext.AiDemoRuns.CountAsync(run => run.CreatedAtUtc >= sinceUtc && (customerId == null || run.CustomerId == customerId), cancellationToken);

    public async Task<PagedResult<AdminCustomerListItemResponse>> AdminCustomersAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.CustomerAccounts.AsNoTracking().Where(account => account.Status != CustomerAccountStatus.Erased);
        if (Enum.TryParse<CustomerAccountStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
        {
            query = query.Where(account => account.Status == parsed);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var pattern = "%" + EscapeLike(search) + "%";
            query = query.Where(account => EF.Functions.ILike(account.Email, pattern, "\\") || EF.Functions.ILike(account.DisplayName, pattern, "\\")
                || (account.Company != null && EF.Functions.ILike(account.Company, pattern, "\\")));
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderByDescending(account => account.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(account => new
            {
                account.Id, account.Email, account.DisplayName, account.Company, account.Status, account.CreatedAtUtc, account.LastSignInAtUtc,
                Open = dbContext.CustomerSubscriptions.Count(subscription => subscription.CustomerId == account.Id
                    && (subscription.Status == SubscriptionStatus.Requested || subscription.Status == SubscriptionStatus.Trial || subscription.Status == SubscriptionStatus.Active)),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return new PagedResult<AdminCustomerListItemResponse>(
            rows.Select(row => new AdminCustomerListItemResponse(row.Id, row.Email, row.DisplayName, row.Company, row.Status.ToString(), row.CreatedAtUtc, row.LastSignInAtUtc, row.Open)).ToList(),
            page, pageSize, total);
    }

    public async Task<AdminCustomerResponse?> AdminCustomerAsync(Guid id, string language, CancellationToken cancellationToken)
    {
        var account = await dbContext.CustomerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.Status != CustomerAccountStatus.Erased, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        var subscriptions = await AdminSubscriptionRows(language).Where(row => row.Subscription.CustomerId == id)
            .OrderByDescending(row => row.Subscription.RequestedAtUtc).ToListAsync(cancellationToken).ConfigureAwait(false);
        var invoices = await AdminInvoicesAsync(null, id, 1, 100, cancellationToken).ConfigureAwait(false);
        return new AdminCustomerResponse(
            account.Id, account.Email, account.DisplayName, account.Company, account.PreferredLanguage, account.Status.ToString(), account.EmailVerifiedAtUtc is not null,
            account.MarketingConsent, account.CreatedAtUtc, account.LastSignInAtUtc, subscriptions.Select(ToAdminSubscription).ToList(), invoices.Items, account.Version);
    }

    public async Task<PagedResult<AdminSubscriptionResponse>> AdminSubscriptionsAsync(string? status, string language, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = AdminSubscriptionRows(language);
        if (Enum.TryParse<SubscriptionStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
        {
            query = query.Where(row => row.Subscription.Status == parsed);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderByDescending(row => row.Subscription.RequestedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<AdminSubscriptionResponse>(rows.Select(ToAdminSubscription).ToList(), page, pageSize, total);
    }

    public async Task<AdminSubscriptionResponse?> AdminSubscriptionAsync(Guid id, string language, CancellationToken cancellationToken) =>
        (await AdminSubscriptionRows(language).Where(row => row.Subscription.Id == id).ToListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToAdminSubscription).SingleOrDefault();

    public async Task<PagedResult<AdminInvoiceListItemResponse>> AdminInvoicesAsync(string? status, Guid? customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = from invoice in dbContext.Invoices.AsNoTracking()
                    join account in dbContext.CustomerAccounts.AsNoTracking() on invoice.CustomerId equals account.Id
                    select new { Invoice = invoice, account.DisplayName };
        if (Enum.TryParse<InvoiceStatus>(status, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed))
        {
            query = query.Where(row => row.Invoice.Status == parsed);
        }

        if (customerId is { } owner)
        {
            query = query.Where(row => row.Invoice.CustomerId == owner);
        }

        var total = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderByDescending(row => row.Invoice.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<AdminInvoiceListItemResponse>(
            rows.Select(row => new AdminInvoiceListItemResponse(
                row.Invoice.Id, row.Invoice.Number, row.Invoice.CustomerId, row.Invoice.BillToName ?? row.DisplayName, row.Invoice.Status.ToString(),
                row.Invoice.Currency, row.Invoice.Total, row.Invoice.DueOn, row.Invoice.IssuedAtUtc, row.Invoice.PaidAtUtc)).ToList(),
            page, pageSize, total);
    }

    public async Task<AdminInvoiceResponse?> AdminInvoiceAsync(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices.AsNoTracking().Include(candidate => candidate.Lines)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return null;
        }

        var account = await dbContext.CustomerAccounts.AsNoTracking().Where(candidate => candidate.Id == invoice.CustomerId)
            .Select(candidate => new { candidate.DisplayName, candidate.Email }).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new AdminInvoiceResponse(
            invoice.Id, invoice.Number, invoice.CustomerId, account.DisplayName, account.Email, invoice.SubscriptionId, invoice.Status.ToString(), invoice.Currency,
            invoice.Total, invoice.DueOn, invoice.Notes, invoice.BillToName, invoice.BillToEmail, invoice.IssuedAtUtc, invoice.PaidAtUtc, invoice.VoidedAtUtc,
            Lines(invoice), invoice.Version);
    }

    private IQueryable<SaasProduct> PublishedProducts() =>
        dbContext.SaasProducts.AsNoTracking()
            .Include(product => product.Translations)
            .Include(product => product.Plans).ThenInclude(plan => plan.Translations)
            .AsSplitQuery()
            .Where(product => product.Status == ProductStatus.Published)
            .OrderBy(product => product.SortOrder).ThenBy(product => product.Key);

    private static Dictionary<string, string> Slugs(SaasProduct product) =>
        product.Translations.ToDictionary(translation => translation.Language, translation => translation.Slug, StringComparer.Ordinal);

    private IQueryable<SubscriptionRow> SubscriptionRows(string language) =>
        from subscription in dbContext.CustomerSubscriptions.AsNoTracking()
        from product in dbContext.SaasProductTranslations.Where(translation => translation.ProductId == subscription.ProductId && translation.Language == language).DefaultIfEmpty()
        from plan in dbContext.SaasProductPlanTranslations.Where(translation => translation.PlanId == subscription.PlanId && translation.Language == language).DefaultIfEmpty()
        select new SubscriptionRow
        {
            Subscription = subscription,
            ProductName = product != null ? product.Name : null,
            ProductSlug = product != null ? product.Slug : null,
            PlanName = plan != null ? plan.Name : null,
        };

    private IQueryable<AdminSubscriptionRow> AdminSubscriptionRows(string language) =>
        from row in SubscriptionRows(language)
        join account in dbContext.CustomerAccounts.AsNoTracking() on row.Subscription.CustomerId equals account.Id
        select new AdminSubscriptionRow
        {
            Subscription = row.Subscription,
            ProductName = row.ProductName,
            PlanName = row.PlanName,
            CustomerName = account.DisplayName,
            CustomerEmail = account.Email,
        };

    private IQueryable<DemoRow> DemoRuns(string language) =>
        from run in dbContext.AiDemoRuns.AsNoTracking()
        from product in dbContext.SaasProductTranslations.Where(translation => translation.ProductId == run.ProductId && translation.Language == language).DefaultIfEmpty()
        select new DemoRow { Run = run, ProductName = product != null ? product.Name : null };

    private static AdminSubscriptionResponse ToAdminSubscription(AdminSubscriptionRow row) => new(
        row.Subscription.Id, row.Subscription.CustomerId, row.CustomerName, row.CustomerEmail, row.Subscription.ProductId, row.ProductName ?? string.Empty,
        row.PlanName, row.Subscription.Status.ToString(), row.Subscription.IsTrial, row.Subscription.RequestedAtUtc, row.Subscription.TrialEndsAtUtc,
        row.Subscription.ActivatedAtUtc, row.Subscription.EndsOn, row.Subscription.CustomerNote, row.Subscription.StaffNote, row.Subscription.Version);

    private static AiDemoRunResponse ToDemoRun(DemoRow row) => new(
        row.Run.Id, row.ProductName ?? string.Empty, row.Run.Status.ToString(), row.Run.Prompt, row.Run.Output, row.Run.FailureCode,
        row.Run.CreatedAtUtc, row.Run.CompletedAtUtc);

    private static CustomerInvoiceResponse ToCustomerInvoice(Invoice invoice) => new(
        invoice.Id, invoice.Number ?? string.Empty, invoice.Status.ToString(), invoice.Currency, invoice.Total, invoice.IssuedAtUtc ?? invoice.CreatedAtUtc,
        invoice.DueOn, invoice.PaidAtUtc, invoice.Notes, Lines(invoice));

    private static List<InvoiceLineResponse> Lines(Invoice invoice) =>
        invoice.Lines.OrderBy(line => line.Position).Select(line => new InvoiceLineResponse(line.Description, line.Quantity, line.UnitPrice, line.Amount)).ToList();

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    // Member-initialized (not constructor) rows, so EF Core can keep composing queries over them.
    private sealed class SubscriptionRow
    {
        public required CustomerSubscription Subscription { get; init; }

        public string? ProductName { get; init; }

        public string? ProductSlug { get; init; }

        public string? PlanName { get; init; }
    }

    private sealed class AdminSubscriptionRow
    {
        public required CustomerSubscription Subscription { get; init; }

        public string? ProductName { get; init; }

        public string? PlanName { get; init; }

        public required string CustomerName { get; init; }

        public required string CustomerEmail { get; init; }
    }

    private sealed class DemoRow
    {
        public required AiDemoRun Run { get; init; }

        public string? ProductName { get; init; }
    }
}
