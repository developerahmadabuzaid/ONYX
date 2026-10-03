using Onyx.Domain.Common;

namespace Onyx.Domain.Privacy;

/// <summary>
/// Server-side record of a visitor's cookie choices (Necessary is implicit). Versioned so a policy change re-prompts.
/// Identifies the browser only by a random consent id; the IP address is stored only as a keyed hash, if at all.
/// </summary>
public sealed class CookieConsentReceipt : Entity
{
    public const int PolicyVersionMaxLength = 20;
    public const int IpHashLength = 64;

    private CookieConsentReceipt()
    {
        PolicyVersion = string.Empty;
        LanguageCode = string.Empty;
    }

    public Guid ConsentId { get; private set; }

    public string PolicyVersion { get; private set; }

    public bool Analytics { get; private set; }

    public bool Marketing { get; private set; }

    public string LanguageCode { get; private set; }

    public DateTimeOffset RecordedAtUtc { get; private set; }

    public string? IpHash { get; private set; }

    public static CookieConsentReceipt Record(
        Guid consentId, string policyVersion, bool analytics, bool marketing, string languageCode, DateTimeOffset recordedAtUtc, string? ipHash)
    {
        if (consentId == Guid.Empty)
        {
            throw new ArgumentException("A consent id is required.", nameof(consentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Consent timestamps must be UTC.", nameof(recordedAtUtc));
        }

        return new CookieConsentReceipt
        {
            ConsentId = consentId,
            PolicyVersion = policyVersion,
            Analytics = analytics,
            Marketing = marketing,
            LanguageCode = languageCode,
            RecordedAtUtc = recordedAtUtc,
            IpHash = ipHash,
        };
    }
}
