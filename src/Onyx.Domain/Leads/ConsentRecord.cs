using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Leads;

/// <summary>
/// The consent given with one inquiry (CLAUDE.md §10): consent to be contacted about the request (required) and marketing
/// consent (optional, separate, withdrawable). <see cref="ConsentVersion"/> identifies the notice wording the visitor saw.
/// </summary>
public sealed class ConsentRecord : AuditableEntity, IAuditable
{
    public const int VersionMaxLength = 20;

    private ConsentRecord()
    {
        ConsentVersion = string.Empty;
        LanguageCode = string.Empty;
    }

    public Guid InquiryId { get; private set; }

    public Guid LeadId { get; private set; }

    public bool ContactConsent { get; private set; }

    public bool MarketingConsent { get; private set; }

    public string ConsentVersion { get; private set; }

    public string LanguageCode { get; private set; }

    public DateTimeOffset GivenAtUtc { get; private set; }

    public DateTimeOffset? MarketingWithdrawnAtUtc { get; private set; }

    public Guid? MarketingWithdrawnById { get; private set; }

    public bool HasMarketingConsent => MarketingConsent && MarketingWithdrawnAtUtc is null;

    public AuditCategory AuditCategory => AuditCategory.Privacy;

    public static Result<ConsentRecord> Give(Guid inquiryId, Guid leadId, bool contactConsent, bool marketingConsent, string consentVersion, string languageCode, DateTimeOffset now)
    {
        if (!contactConsent)
        {
            return Error.Validation("Inquiry.ContactConsentRequired", "Please agree to be contacted about your request.");
        }

        if (string.IsNullOrWhiteSpace(consentVersion) || consentVersion.Length > VersionMaxLength)
        {
            return Error.Validation("Inquiry.ConsentVersion", "The consent notice version is missing.");
        }

        return new ConsentRecord
        {
            InquiryId = inquiryId,
            LeadId = leadId,
            ContactConsent = true,
            MarketingConsent = marketingConsent,
            ConsentVersion = consentVersion,
            LanguageCode = languageCode,
            GivenAtUtc = now,
        };
    }

    public Result WithdrawMarketing(Guid? withdrawnById, DateTimeOffset now)
    {
        if (!HasMarketingConsent)
        {
            return Error.Conflict("Consent.NotGiven", "There is no active marketing consent to withdraw.");
        }

        MarketingWithdrawnAtUtc = now;
        MarketingWithdrawnById = withdrawnById;
        return Result.Success();
    }
}
