using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Commerce;
using Onyx.Contracts.Products;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Products;

namespace Onyx.Application.Products;

/// <summary>
/// Admin management of the product catalogue (permissions are enforced by the API). Publishing requires a recorded
/// verification and complete Arabic and English copy; prices are entered by staff and never invented.
/// </summary>
public sealed class ProductAdministrationService(
    ISaasProductRepository products,
    ICommerceReadModel readModel,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<IReadOnlyList<AdminProductListItemResponse>> ListAsync(string language, CancellationToken cancellationToken) =>
        readModel.AdminProductsAsync(CommerceErrors.Language(language), cancellationToken);

    public async Task<Result<AdminProductResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await readModel.AdminProductAsync(id, cancellationToken).ConfigureAwait(false) is { } product ? product : CommerceErrors.ProductNotFound;

    public async Task<Result<AdminProductResponse>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = SaasProduct.Create(request.Key);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        if (await products.KeyExistsAsync(created.Value.Key, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("Product.DuplicateKey", "Another product uses this key.");
        }

        products.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(created.Value.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminProductResponse>> SaveAsync(Guid id, SaveProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (product.IsFailure)
        {
            return product.Error!;
        }

        if (!Enum.TryParse<DemoKind>(request.DemoKind, ignoreCase: false, out var demoKind) || !Enum.IsDefined(demoKind))
        {
            return Error.Validation("Product.InvalidDemoKind", "Choose a demo type.");
        }

        var configured = product.Value.Configure(request.TrialDays, demoKind, request.DemoUrl, request.SortOrder);
        if (configured.IsFailure)
        {
            return configured.Error!;
        }

        foreach (var translation in request.Translations ?? [])
        {
            var set = product.Value.SetTranslation(translation.Language, translation.Name, translation.Slug, translation.Summary, translation.Description);
            if (set.IsFailure)
            {
                return set.Error!;
            }

            var slug = product.Value.Translation(translation.Language)!.Slug;
            if (await readModel.ProductSlugTakenAsync(translation.Language, slug, product.Value.Id, cancellationToken).ConfigureAwait(false))
            {
                return Error.Conflict("Product.DuplicateSlug", "Another product uses this slug.");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminProductResponse>> AddPlanAsync(Guid id, SavePlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (product.IsFailure)
        {
            return product.Error!;
        }

        if (!TryParsePeriod(request.BillingPeriod, out var period))
        {
            return InvalidPeriod;
        }

        var added = product.Value.AddPlan(request.Code, Text(request), request.Price, request.Currency, period, request.IsPricePublic, request.SortOrder);
        if (added.IsFailure)
        {
            return added.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminProductResponse>> UpdatePlanAsync(Guid id, Guid planId, SavePlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (product.IsFailure)
        {
            return product.Error!;
        }

        if (!TryParsePeriod(request.BillingPeriod, out var period))
        {
            return InvalidPeriod;
        }

        var updated = product.Value.UpdatePlan(planId, Text(request), request.Price, request.Currency, period, request.IsPricePublic, request.IsActive, request.SortOrder);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminProductResponse>> VerifyAsync(Guid id, VerifyProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("Product.NoActor", "Sign in again.");
        }

        var product = await LoadAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (product.IsFailure)
        {
            return product.Error!;
        }

        var verified = product.Value.RecordVerification(userId, request.Source, timeProvider.GetUtcNow());
        if (verified.IsFailure)
        {
            return verified.Error!;
        }

        auditTrail.Record(AuditAction.ProductVerified, AuditCategory.Commerce, nameof(SaasProduct), id.ToString(), new { product = product.Value.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminProductResponse>> PublishAsync(Guid id, ProductVersionRequest request, bool publish, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var product = await LoadAsync(id, request.Version, cancellationToken).ConfigureAwait(false);
        if (product.IsFailure)
        {
            return product.Error!;
        }

        if (publish)
        {
            var published = product.Value.Publish(timeProvider.GetUtcNow());
            if (published.IsFailure)
            {
                return published.Error!;
            }
        }
        else
        {
            product.Value.Unpublish();
        }

        auditTrail.Record(publish ? AuditAction.ProductPublished : AuditAction.ProductUnpublished, AuditCategory.Publishing, nameof(SaasProduct), id.ToString(),
            new { product = product.Value.Key });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private static readonly Error InvalidPeriod = Error.Validation("Product.InvalidBillingPeriod", "Choose a billing period.");

    private static bool TryParsePeriod(string? value, out BillingPeriod period) =>
        Enum.TryParse(value, ignoreCase: false, out period) && Enum.IsDefined(period);

    private static Dictionary<string, (string? Name, string? Description)> Text(SavePlanRequest request) =>
        (request.Translations ?? []).GroupBy(translation => translation.Language, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (group.Last().Name, group.Last().Description), StringComparer.Ordinal);

    private async Task<Result<SaasProduct>> LoadAsync(Guid id, uint version, CancellationToken cancellationToken)
    {
        var product = await products.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (product is null)
        {
            return CommerceErrors.ProductNotFound;
        }

        products.SetExpectedVersion(product, version);
        return product;
    }
}
