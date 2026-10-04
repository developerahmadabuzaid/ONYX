using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Knowledge;

/// <summary>
/// One piece of Approved Company Knowledge: a verified statement about Onyx, written per language
/// (<see cref="LocalizedText.Text"/>), with the source of the verification. Future AI writing is grounded only in approved
/// entries; nothing is pre-populated, and any edit sends an approved entry back to draft for re-verification.
/// </summary>
public sealed class KnowledgeEntry : AuditableEntity, IAuditable
{
    public const int TitleMaxLength = 200;
    public const int StatementMaxLength = 4_000;
    public const int SourceMaxLength = 1_000;

    private KnowledgeEntry()
    {
        Title = string.Empty;
        Statements = [];
    }

    public KnowledgeCategory Category { get; private set; }

    public string Title { get; private set; }

    public IReadOnlyList<LocalizedText> Statements { get; private set; }

    /// <summary>Where the fact comes from (owner confirmation, contract, delivered system…).</summary>
    public string? SourceNote { get; private set; }

    public KnowledgeStatus Status { get; private set; }

    public Guid? VerifiedById { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public static Result<KnowledgeEntry> Create(KnowledgeCategory category, string title, IReadOnlyList<LocalizedText> statements, string? sourceNote)
    {
        var entry = new KnowledgeEntry { Status = KnowledgeStatus.Draft };
        var updated = entry.Update(category, title, statements, sourceNote);
        return updated.IsSuccess ? entry : updated.Error!;
    }

    public Result Update(KnowledgeCategory category, string title, IReadOnlyList<LocalizedText> statements, string? sourceNote)
    {
        ArgumentNullException.ThrowIfNull(statements);
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(category) || trimmedTitle.Length is 0 or > TitleMaxLength)
        {
            return Error.Validation("Knowledge.Invalid", $"A category and a title of 1-{TitleMaxLength} characters are required.");
        }

        if (statements.Count == 0 || statements.GroupBy(statement => statement.LanguageCode).Any(group => group.Count() > 1)
            || statements.Any(statement => string.IsNullOrWhiteSpace(statement.Text) || statement.Text.Length > StatementMaxLength))
        {
            return Error.Validation("Knowledge.InvalidStatements", $"At least one statement, one per language, max {StatementMaxLength} characters.");
        }

        if (sourceNote is { Length: > SourceMaxLength })
        {
            return Error.Validation("Knowledge.InvalidSource", $"The source note is limited to {SourceMaxLength} characters.");
        }

        if (Status == KnowledgeStatus.Retired)
        {
            return Error.Conflict("Knowledge.Retired", "Retired knowledge cannot be edited.");
        }

        Category = category;
        Title = trimmedTitle;
        Statements = statements.Select(statement => statement with { Text = statement.Text.Trim(), Secondary = null }).ToList();
        SourceNote = string.IsNullOrWhiteSpace(sourceNote) ? null : sourceNote.Trim();
        Status = KnowledgeStatus.Draft;
        VerifiedById = null;
        VerifiedAtUtc = null;
        return Result.Success();
    }

    /// <summary>Verification needs a recorded source: unsourced statements can never become approved knowledge.</summary>
    public Result Approve(Guid verifiedById, DateTimeOffset now)
    {
        if (Status != KnowledgeStatus.Draft)
        {
            return Error.Conflict("Knowledge.InvalidTransition", "Only draft knowledge can be approved.");
        }

        if (string.IsNullOrWhiteSpace(SourceNote))
        {
            return Error.Validation("Knowledge.SourceRequired", "Record where this fact comes from before approving it.");
        }

        Status = KnowledgeStatus.Approved;
        VerifiedById = verifiedById;
        VerifiedAtUtc = now;
        return Result.Success();
    }

    public Result Retire()
    {
        if (Status == KnowledgeStatus.Retired)
        {
            return Error.Conflict("Knowledge.InvalidTransition", "Already retired.");
        }

        Status = KnowledgeStatus.Retired;
        return Result.Success();
    }
}
