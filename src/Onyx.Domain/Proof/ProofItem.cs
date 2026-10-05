using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Proof;

/// <summary>
/// A capability, metric or testimonial shown on a project or case study. Every item is public proof: it is shown only once it has
/// a recorded verification and was published, and any edit takes it back to an unverified draft. Text is per language
/// (<see cref="LocalizedText.Text"/> = name, label or quote; <see cref="LocalizedText.Secondary"/> = description); a language
/// without text simply does not show the item.
/// </summary>
public sealed class ProofItem : AuditableEntity, IAuditable
{
    public const int TitleMaxLength = 300;
    public const int QuoteMaxLength = 1_500;
    public const int DescriptionMaxLength = 2_000;
    public const int ValueMaxLength = 40;
    public const int AttributionMaxLength = 200;

    private ProofItem()
    {
        Texts = [];
    }

    public Guid ContentItemId { get; private set; }

    public ProofItemKind Kind { get; private set; }

    public IReadOnlyList<LocalizedText> Texts { get; private set; }

    /// <summary>Metrics only: the measured value exactly as verified (e.g. "35%").</summary>
    public string? Value { get; private set; }

    /// <summary>Metrics only: the date the value was true.</summary>
    public DateOnly? AsOf { get; private set; }

    /// <summary>Testimonials only: who said it, with their role and organization as the client permitted.</summary>
    public string? AttributionName { get; private set; }

    public string? AttributionRole { get; private set; }

    public string? AttributionOrganization { get; private set; }

    public int SortOrder { get; private set; }

    public ProofItemStatus Status { get; private set; }

    /// <remarks>Not audited by value (notes may name client contacts); verification is audited explicitly.</remarks>
    [NotAudited]
    public ProofVerification? Verification { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public static Result<ProofItem> Create(
        Guid contentItemId, ProofItemKind kind, IReadOnlyList<LocalizedText> texts, string? value, DateOnly? asOf,
        string? attributionName, string? attributionRole, string? attributionOrganization, int sortOrder)
    {
        if (!Enum.IsDefined(kind))
        {
            return Error.Validation("Proof.InvalidKind", "Unknown proof kind.");
        }

        var item = new ProofItem { ContentItemId = contentItemId, Kind = kind, Status = ProofItemStatus.Draft };
        var updated = item.Update(texts, value, asOf, attributionName, attributionRole, attributionOrganization, sortOrder);
        return updated.IsSuccess ? item : updated.Error!;
    }

    /// <summary>Replaces the wording. The item returns to an unverified draft: changed proof must be verified again.</summary>
    public Result Update(
        IReadOnlyList<LocalizedText> texts, string? value, DateOnly? asOf, string? attributionName, string? attributionRole, string? attributionOrganization, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var cleaned = texts
            .Where(text => !string.IsNullOrWhiteSpace(text.Text))
            .Select(text => new LocalizedText(text.LanguageCode, text.Text.Trim(), string.IsNullOrWhiteSpace(text.Secondary) ? null : text.Secondary.Trim()))
            .ToList();
        var titleLimit = Kind == ProofItemKind.Testimonial ? QuoteMaxLength : TitleMaxLength;
        if (cleaned.Count == 0 || cleaned.Select(text => text.LanguageCode).Distinct(StringComparer.Ordinal).Count() != cleaned.Count
            || cleaned.Any(text => text.Text.Length > titleLimit || text.Secondary is { Length: > DescriptionMaxLength }
                || ContentBodyRules.HasControlCharacters(text.Text, allowNewLines: true)))
        {
            return Error.Validation("Proof.InvalidText", "Write the item in at least one language, once per language, within the length limits.");
        }

        var trimmedValue = Clean(value);
        var name = Clean(attributionName);
        var role = Clean(attributionRole);
        var organization = Clean(attributionOrganization);
        var kindError = Kind switch
        {
            ProofItemKind.Metric when trimmedValue is null || trimmedValue.Length > ValueMaxLength || asOf is null =>
                Error.Validation("Proof.MetricIncomplete", "A metric needs its verified value and the date it was true."),
            ProofItemKind.Testimonial when name is null =>
                Error.Validation("Proof.AttributionRequired", "A testimonial needs the name of the person quoted."),
            not ProofItemKind.Metric when trimmedValue is not null || asOf is not null =>
                Error.Validation("Proof.UnexpectedValue", "Only metrics have a value and a date."),
            not ProofItemKind.Testimonial when name is not null || role is not null || organization is not null =>
                Error.Validation("Proof.UnexpectedAttribution", "Only testimonials are attributed."),
            _ when new[] { name, role, organization }.Any(part => part is { Length: > AttributionMaxLength }) =>
                Error.Validation("Proof.AttributionTooLong", $"Attribution parts are limited to {AttributionMaxLength} characters."),
            _ => null,
        };
        if (kindError is not null)
        {
            return kindError;
        }

        Texts = cleaned;
        Value = trimmedValue;
        AsOf = asOf;
        AttributionName = name;
        AttributionRole = role;
        AttributionOrganization = organization;
        SortOrder = sortOrder;
        Verification = null;
        Status = ProofItemStatus.Draft;
        PublishedAtUtc = null;
        return Result.Success();
    }

    /// <summary>Records the verification. Testimonials always need the client's recorded permission.</summary>
    public Result Verify(Guid verifiedById, DateTimeOffset now, string? sourceNote, ClientPermission clientPermission, string? clientPermissionNote)
    {
        var verification = ProofVerification.Create(
            verifiedById, now, sourceNote, clientPermission, clientPermissionNote, clientPermissionRequired: Kind == ProofItemKind.Testimonial);
        if (verification.IsFailure)
        {
            return verification.Error!;
        }

        Verification = verification.Value;
        return Result.Success();
    }

    public Result Publish(DateTimeOffset now)
    {
        if (Verification is null)
        {
            return Error.Validation("Proof.NotVerified", "Verify this proof before publishing it.");
        }

        if (Status == ProofItemStatus.Published)
        {
            return Error.Conflict("Proof.AlreadyPublished", "This item is already published.");
        }

        Status = ProofItemStatus.Published;
        PublishedAtUtc = now;
        return Result.Success();
    }

    public Result Unpublish()
    {
        if (Status != ProofItemStatus.Published)
        {
            return Error.Conflict("Proof.NotPublished", "This item is not published.");
        }

        Status = ProofItemStatus.Draft;
        PublishedAtUtc = null;
        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
