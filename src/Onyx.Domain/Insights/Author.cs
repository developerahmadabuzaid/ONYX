using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Insights;

/// <summary>
/// A real person who may be credited on insights. Names are per language (<see cref="LocalizedText.Text"/> = name,
/// <see cref="LocalizedText.Secondary"/> = job title). The person's agreement to be named publicly is recorded; there are no
/// invented personas, and an insight without an author is credited to the organization.
/// </summary>
public sealed class Author : AuditableEntity, IAuditable
{
    public const int NameMaxLength = 150;
    public const int TitleMaxLength = 150;
    public const int ConsentMaxLength = 1_000;

    private Author()
    {
        Names = [];
        ConsentNote = string.Empty;
    }

    public IReadOnlyList<LocalizedText> Names { get; private set; }

    /// <summary>How and when the person agreed to be credited publicly.</summary>
    [NotAudited]
    public string ConsentNote { get; private set; }

    public bool IsActive { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Content;

    public static Result<Author> Create(IReadOnlyList<LocalizedText> names, string? consentNote)
    {
        var author = new Author { IsActive = true };
        var updated = author.Update(names, consentNote, isActive: true);
        return updated.IsSuccess ? author : updated.Error!;
    }

    public Result Update(IReadOnlyList<LocalizedText> names, string? consentNote, bool isActive)
    {
        ArgumentNullException.ThrowIfNull(names);
        var cleaned = names
            .Where(name => !string.IsNullOrWhiteSpace(name.Text))
            .Select(name => new LocalizedText(name.LanguageCode, name.Text.Trim(), string.IsNullOrWhiteSpace(name.Secondary) ? null : name.Secondary.Trim()))
            .ToList();
        if (cleaned.Count == 0 || cleaned.Select(name => name.LanguageCode).Distinct(StringComparer.Ordinal).Count() != cleaned.Count
            || cleaned.Any(name => name.Text.Length > NameMaxLength || name.Secondary is { Length: > TitleMaxLength }
                || ContentBodyRules.HasControlCharacters(name.Text, allowNewLines: false)))
        {
            return Error.Validation("Author.InvalidName", "Enter the person's name in at least one language, once per language.");
        }

        var consent = consentNote?.Trim();
        if (string.IsNullOrEmpty(consent) || consent.Length > ConsentMaxLength)
        {
            return Error.Validation("Author.ConsentRequired", "Record how and when this person agreed to be credited publicly.");
        }

        Names = cleaned;
        ConsentNote = consent;
        IsActive = isActive;
        return Result.Success();
    }
}
