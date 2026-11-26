using System.Text.RegularExpressions;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Products;

/// <summary>
/// A SaaS product offered to customers (docs/architecture.md §24.8). Publishing needs complete Arabic and English copy and a
/// recorded verification that the product, its description and its demo are real (proof policy, CLAUDE.md §2.1). Plans belong
/// to the product aggregate, so the product's version guards concurrent edits of either.
/// </summary>
public sealed partial class SaasProduct : AuditableEntity, IAuditable
{
    public const int KeyMaxLength = 40;
    public const int DemoUrlMaxLength = 500;
    public const int VerificationSourceMaxLength = 300;
    public const int MaxTrialDays = 90;
    public const int MaxPlans = 12;

    public static readonly IReadOnlyList<string> Languages = ["ar", "en"];

    private readonly List<ProductTranslation> _translations = [];
    private readonly List<ProductPlan> _plans = [];

    private SaasProduct()
    {
        Key = string.Empty;
    }

    /// <summary>Stable internal identifier (a–z, 0–9, hyphens); never shown to visitors and never changed.</summary>
    public string Key { get; private set; }

    public ProductStatus Status { get; private set; }

    /// <summary>Trial length in days, or null when the product has no self-service trial.</summary>
    public int? TrialDays { get; private set; }

    public DemoKind DemoKind { get; private set; }

    public string? DemoUrl { get; private set; }

    public int SortOrder { get; private set; }

    public Guid? VerifiedById { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    /// <summary>Where the facts were confirmed (e.g. "Owner confirmation 2026-10-27; live product demo").</summary>
    public string? VerificationSource { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<ProductTranslation> Translations => _translations.AsReadOnly();

    public IReadOnlyCollection<ProductPlan> Plans => _plans.AsReadOnly();

    public AuditCategory AuditCategory => AuditCategory.Commerce;

    public bool IsVerified => VerifiedAtUtc is not null;

    public bool OffersTrial => TrialDays is > 0;

    public static Result<SaasProduct> Create(string? key)
    {
        var clean = key?.Trim().ToLowerInvariant();
        if (clean is not { Length: > 0 and <= KeyMaxLength } || !KeyPattern().IsMatch(clean))
        {
            return Error.Validation("Product.InvalidKey", "Product keys use a–z, 0–9 and hyphens (at most 40 characters).");
        }

        return new SaasProduct { Key = clean, Status = ProductStatus.Draft };
    }

    public ProductTranslation? Translation(string language) => _translations.SingleOrDefault(translation => translation.Language == language);

    /// <summary>Sets one language. Slugs are normalized (CLAUDE.md §7); uniqueness per language is checked by the service.</summary>
    public Result SetTranslation(string language, string? name, string? slug, string? summary, string? description)
    {
        if (!Languages.Contains(language))
        {
            return Error.Validation("Product.InvalidLanguage", "Unsupported language.");
        }

        var cleanName = name?.Trim();
        if (string.IsNullOrEmpty(cleanName) || cleanName.Length > ProductTranslation.NameMaxLength)
        {
            return Error.Validation("Product.InvalidName", $"Enter the product name (at most {ProductTranslation.NameMaxLength} characters).");
        }

        var cleanSlug = Slug.Normalize(string.IsNullOrWhiteSpace(slug) ? cleanName : slug);
        if (!Slug.IsValid(cleanSlug))
        {
            return Error.Validation("Product.InvalidSlug", "Enter a valid URL slug.");
        }

        var cleanSummary = summary?.Trim() ?? string.Empty;
        var cleanDescription = description?.Trim() ?? string.Empty;
        if (cleanSummary.Length > ProductTranslation.SummaryMaxLength || cleanDescription.Length > ProductTranslation.DescriptionMaxLength)
        {
            return Error.Validation("Product.TextTooLong",
                $"The summary is at most {ProductTranslation.SummaryMaxLength} characters and the description at most {ProductTranslation.DescriptionMaxLength}.");
        }

        if (Status == ProductStatus.Published && (cleanSummary.Length == 0 || cleanDescription.Length == 0))
        {
            return Error.Validation("Product.IncompleteTranslation", "A published product needs a summary and a description in both languages.");
        }

        var translation = Translation(language);
        if (translation is null)
        {
            translation = ProductTranslation.Create(Id, language);
            _translations.Add(translation);
        }

        translation.Set(cleanName, cleanSlug, cleanSummary, cleanDescription);
        return Result.Success();
    }

    public Result Configure(int? trialDays, DemoKind demoKind, string? demoUrl, int sortOrder)
    {
        if (trialDays is { } days && (days < 1 || days > MaxTrialDays))
        {
            return Error.Validation("Product.InvalidTrial", $"A trial lasts 1 to {MaxTrialDays} days, or leave it empty for no trial.");
        }

        var cleanUrl = demoUrl?.Trim();
        cleanUrl = string.IsNullOrEmpty(cleanUrl) ? null : cleanUrl;
        var needsUrl = demoKind is DemoKind.Video or DemoKind.GuidedTour or DemoKind.Sandbox;
        if (needsUrl && !IsHttpsUrl(cleanUrl))
        {
            return Error.Validation("Product.InvalidDemoUrl", "This demo type needs an https link without credentials.");
        }

        DemoKind = demoKind;
        DemoUrl = needsUrl ? cleanUrl : null;
        TrialDays = trialDays;
        SortOrder = sortOrder;
        return Result.Success();
    }

    /// <summary>Records who confirmed that the product, its copy and its demo are real, and on what basis.</summary>
    public Result RecordVerification(Guid verifiedById, string? source, DateTimeOffset nowUtc)
    {
        var clean = source?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > VerificationSourceMaxLength)
        {
            return Error.Validation("Product.InvalidVerification", $"Describe how the facts were confirmed (at most {VerificationSourceMaxLength} characters).");
        }

        VerifiedById = verifiedById;
        VerifiedAtUtc = nowUtc;
        VerificationSource = clean;
        return Result.Success();
    }

    public Result Publish(DateTimeOffset nowUtc)
    {
        if (Status == ProductStatus.Published)
        {
            return Result.Success();
        }

        if (!IsVerified)
        {
            return Error.Conflict("Product.Unverified", "Record a verification before publishing (proof policy).");
        }

        foreach (var language in Languages)
        {
            if (Translation(language) is not { Summary.Length: > 0, Description.Length: > 0 })
            {
                return Error.Conflict("Product.IncompleteTranslation", "A published product needs a summary and a description in both languages.");
            }
        }

        Status = ProductStatus.Published;
        PublishedAtUtc ??= nowUtc;
        return Result.Success();
    }

    public void Unpublish() => Status = Status == ProductStatus.Published ? ProductStatus.Draft : Status;

    public void Archive() => Status = ProductStatus.Archived;

    public Result<ProductPlan> AddPlan(
        string? code, IReadOnlyDictionary<string, (string? Name, string? Description)> text,
        decimal? price, string? currency, BillingPeriod period, bool isPricePublic, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_plans.Count >= MaxPlans)
        {
            return Error.Conflict("Product.TooManyPlans", $"A product has at most {MaxPlans} plans.");
        }

        var cleanCode = ProductPlan.ValidateCode(code);
        if (cleanCode.IsFailure)
        {
            return cleanCode.Error!;
        }

        if (_plans.Any(plan => plan.Code == cleanCode.Value))
        {
            return Error.Conflict("Product.DuplicatePlanCode", "Another plan of this product uses that code.");
        }

        var plan = ProductPlan.Create(Id, cleanCode.Value);
        var configured = plan.Configure(text, price, currency, period, isPricePublic, isActive: true, sortOrder);
        if (configured.IsFailure)
        {
            return configured.Error!;
        }

        _plans.Add(plan);
        return plan;
    }

    public Result UpdatePlan(
        Guid planId, IReadOnlyDictionary<string, (string? Name, string? Description)> text,
        decimal? price, string? currency, BillingPeriod period, bool isPricePublic, bool isActive, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(text);
        var plan = _plans.SingleOrDefault(candidate => candidate.Id == planId);
        return plan is null
            ? Error.NotFound("Product.PlanNotFound", "Plan not found.")
            : plan.Configure(text, price, currency, period, isPricePublic, isActive, sortOrder);
    }

    public ProductPlan? FindPlan(Guid planId) => _plans.SingleOrDefault(plan => plan.Id == planId);

    private static bool IsHttpsUrl(string? value) =>
        value is { Length: <= DemoUrlMaxLength } && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
