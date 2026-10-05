using Onyx.Domain.Common;
using Onyx.Domain.Content;

namespace Onyx.Domain.Leads;

/// <summary>An internal staff note on a lead. Notes are kept as written (append-only) so the history stays trustworthy.</summary>
public sealed class LeadNote : Entity
{
    public const int BodyMaxLength = 4_000;

    private LeadNote()
    {
        Body = string.Empty;
    }

    public Guid LeadId { get; private set; }

    [SensitiveData]
    public string Body { get; private set; }

    public Guid AuthorId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static Result<LeadNote> Write(Guid leadId, string? body, Guid authorId, DateTimeOffset now)
    {
        var clean = body?.Trim() ?? string.Empty;
        if (clean.Length is 0 or > BodyMaxLength || ContentBodyRules.HasControlCharacters(clean, allowNewLines: true))
        {
            return Error.Validation("Lead.InvalidNote", $"Notes are 1 to {BodyMaxLength} characters.");
        }

        return new LeadNote { LeadId = leadId, Body = clean, AuthorId = authorId, CreatedAtUtc = now };
    }
}
