using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Products;

/// <summary>
/// A plan of a product. The price is optional: it is shown publicly only when an administrator has entered a real amount and
/// marked it public (CLAUDE.md §2.4). Custom-period plans are always "contact us" and never show a price.
/// </summary>
public sealed partial class ProductPlan : Entity
{
    public const int CodeMaxLength = 40;
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 1000;
    public const decimal MaxPrice = 10_000_000m;

    private readonly List<ProductPlanTranslation> _translations = [];

    private ProductPlan()
    {
        Code = string.Empty;
    }

    public Guid ProductId { get; private set; }

    public string Code { get; private set; }

    public decimal? Price { get; private set; }

    /// <summary>ISO 4217 code, e.g. SAR, JOD, USD.</summary>
    public string? Currency { get; private set; }

    public BillingPeriod BillingPeriod { get; private set; }

    public bool IsPricePublic { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyCollection<ProductPlanTranslation> Translations => _translations.AsReadOnly();

    /// <summary>True when visitors may see the amount.</summary>
    public bool ShowsPrice => IsPricePublic && Price is > 0 && Currency is not null && BillingPeriod != BillingPeriod.Custom;

    internal static ProductPlan Create(Guid productId, string code) => new() { ProductId = productId, Code = code, IsActive = true };

    internal static Result<string> ValidateCode(string? code)
    {
        var clean = code?.Trim().ToLowerInvariant();
        return clean is { Length: > 0 and <= CodeMaxLength } && CodePattern().IsMatch(clean)
            ? clean
            : Error.Validation("Product.InvalidPlanCode", "Plan codes use a–z, 0–9 and hyphens (at most 40 characters).");
    }

    internal Result Configure(
        IReadOnlyDictionary<string, (string? Name, string? Description)> text,
        decimal? price, string? currency, BillingPeriod period, bool isPricePublic, bool isActive, int sortOrder)
    {
        if (price is { } amount && (amount <= 0 || amount > MaxPrice || decimal.Round(amount, 2) != amount))
        {
            return Error.Validation("Product.InvalidPrice", "Enter a positive price with at most two decimals.");
        }

        var cleanCurrency = currency?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(cleanCurrency) && !CurrencyPattern().IsMatch(cleanCurrency))
        {
            return Error.Validation("Product.InvalidCurrency", "Use a three-letter currency code (for example SAR, JOD or USD).");
        }

        cleanCurrency = string.IsNullOrEmpty(cleanCurrency) ? null : cleanCurrency;
        if (isPricePublic && (price is null || cleanCurrency is null || period == BillingPeriod.Custom))
        {
            return Error.Validation("Product.PriceNotPublishable", "A public price needs an amount, a currency and a billing period other than custom.");
        }

        foreach (var language in SaasProduct.Languages)
        {
            var (name, description) = text.TryGetValue(language, out var value) ? value : (null, null);
            var cleanName = name?.Trim();
            var cleanDescription = description?.Trim();
            if (string.IsNullOrEmpty(cleanName) || cleanName.Length > NameMaxLength)
            {
                return Error.Validation("Product.InvalidPlanName", $"Name the plan in Arabic and English (at most {NameMaxLength} characters).");
            }

            if (cleanDescription is { Length: > DescriptionMaxLength })
            {
                return Error.Validation("Product.InvalidPlanDescription", $"Plan descriptions are at most {DescriptionMaxLength} characters.");
            }

            var translation = _translations.SingleOrDefault(candidate => candidate.Language == language);
            if (translation is null)
            {
                translation = ProductPlanTranslation.Create(Id, language);
                _translations.Add(translation);
            }

            translation.Set(cleanName, string.IsNullOrEmpty(cleanDescription) ? null : cleanDescription);
        }

        Price = price;
        Currency = cleanCurrency;
        BillingPeriod = period;
        IsPricePublic = isPricePublic;
        IsActive = isActive;
        SortOrder = sortOrder;
        return Result.Success();
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
