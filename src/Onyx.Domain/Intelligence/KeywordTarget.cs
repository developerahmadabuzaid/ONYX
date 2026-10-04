using System.Text.RegularExpressions;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// One row of the Keyword &amp; Content Map: keyword → intent → language → market → target content → cluster → status.
/// Holds no volume, difficulty or ranking: performance is read from provider snapshots, with provenance, when they exist.
/// One keyword per language and market can be owned by only one target, which is how cannibalization is prevented.
/// </summary>
public sealed partial class KeywordTarget : AuditableEntity, IAuditable
{
    public const int KeywordMaxLength = 150;
    public const int NotesMaxLength = 1_000;

    private KeywordTarget()
    {
        Keyword = string.Empty;
        NormalizedKeyword = string.Empty;
        LanguageCode = string.Empty;
        TargetMarket = string.Empty;
    }

    public string Keyword { get; private set; }

    public string NormalizedKeyword { get; private set; }

    public string LanguageCode { get; private set; }

    /// <summary>ISO country code, or empty for "all markets".</summary>
    public string TargetMarket { get; private set; }

    public SearchIntent SearchIntent { get; private set; }

    public Guid? TargetContentItemId { get; private set; }

    public Guid? ClusterId { get; private set; }

    public KeywordStatus Status { get; private set; }

    public string? Notes { get; private set; }

    public uint Version { get; private set; }

    public static Result<KeywordTarget> Create(string keyword, string languageCode, string? targetMarket, SearchIntent intent)
    {
        var trimmed = keyword?.Trim() ?? string.Empty;
        var market = targetMarket?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > KeywordMaxLength || ContentBodyRules.HasControlCharacters(trimmed, false))
        {
            return Error.Validation("Keyword.Invalid", $"A keyword of 1-{KeywordMaxLength} characters is required.");
        }

        if (market.Length > 0 && !MarketCode().IsMatch(market))
        {
            return Error.Validation("Keyword.InvalidMarket", "The market must be an ISO 3166-1 alpha-2 country code, e.g. SA.");
        }

        var target = new KeywordTarget
        {
            Keyword = trimmed,
            NormalizedKeyword = TextNormalization.NormalizeKeyword(trimmed),
            LanguageCode = languageCode,
            TargetMarket = market,
            Status = KeywordStatus.Planned,
        };
        var updated = target.Update(intent, null, null, KeywordStatus.Planned, null);
        return updated.IsSuccess ? target : updated.Error!;
    }

    public Result Update(SearchIntent intent, Guid? targetContentItemId, Guid? clusterId, KeywordStatus status, string? notes)
    {
        if (!Enum.IsDefined(intent) || !Enum.IsDefined(status) || notes is { Length: > NotesMaxLength })
        {
            return Error.Validation("Keyword.Invalid", "Unknown intent or status, or notes too long.");
        }

        if (status is KeywordStatus.Targeted or KeywordStatus.Monitoring && targetContentItemId is null)
        {
            return Error.Validation("Keyword.TargetRequired", "A targeted keyword needs the content that targets it.");
        }

        SearchIntent = intent;
        TargetContentItemId = targetContentItemId;
        ClusterId = clusterId;
        Status = status;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        return Result.Success();
    }

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MarketCode();
}
