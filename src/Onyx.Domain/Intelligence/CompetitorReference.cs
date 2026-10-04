using System.Text.RegularExpressions;
using Onyx.Domain.Common;

namespace Onyx.Domain.Intelligence;

/// <summary>
/// An approved competitor or reference domain for internal research only. Never rendered publicly, never copied from, and
/// never used for public comparisons without explicit owner approval. No crawling is implemented; future analysis must use
/// lawful, approved data sources.
/// </summary>
public sealed partial class CompetitorReference : AuditableEntity, IAuditable
{
    public const int NameMaxLength = 150;
    public const int DomainMaxLength = 253;
    public const int NotesMaxLength = 2_000;
    public const int MaxListItems = 30;

    private CompetitorReference()
    {
        Name = string.Empty;
        Domain = string.Empty;
        Markets = [];
        Languages = [];
        Topics = [];
    }

    public string Name { get; private set; }

    public string Domain { get; private set; }

    public IReadOnlyList<string> Markets { get; private set; }

    public IReadOnlyList<string> Languages { get; private set; }

    public IReadOnlyList<string> Topics { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    public static Result<CompetitorReference> Create(string name, string domain)
    {
        var normalized = (domain ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length > DomainMaxLength || !DomainPattern().IsMatch(normalized))
        {
            return Error.Validation("Competitor.InvalidDomain", "Enter a bare domain name such as example.com (no scheme or path).");
        }

        var reference = new CompetitorReference { Domain = normalized, IsActive = true };
        var updated = reference.Update(name, [], [], [], null, isActive: true);
        return updated.IsSuccess ? reference : updated.Error!;
    }

    public Result Update(string name, IReadOnlyList<string> markets, IReadOnlyList<string> languages, IReadOnlyList<string> topics, string? notes, bool isActive)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > NameMaxLength || notes is { Length: > NotesMaxLength })
        {
            return Error.Validation("Competitor.Invalid", $"A name of 1-{NameMaxLength} characters is required.");
        }

        if (markets.Count > MaxListItems || languages.Count > MaxListItems || topics.Count > MaxListItems
            || markets.Any(market => !MarketPattern().IsMatch(market)) || topics.Any(topic => string.IsNullOrWhiteSpace(topic) || topic.Length > 150))
        {
            return Error.Validation("Competitor.InvalidLists", "Markets are ISO country codes; topics are short labels.");
        }

        Name = trimmed;
        Markets = markets.Distinct(StringComparer.Ordinal).ToList();
        Languages = languages.Distinct(StringComparer.Ordinal).ToList();
        Topics = topics.Select(topic => topic.Trim()).Distinct(StringComparer.Ordinal).ToList();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsActive = isActive;
        return Result.Success();
    }

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex MarketPattern();
}
