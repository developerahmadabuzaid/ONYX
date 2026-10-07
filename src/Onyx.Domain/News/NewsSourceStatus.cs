namespace Onyx.Domain.News;

/// <summary>Stored as text. Only allowlisted sources are fetched; a new source starts blocked until its terms are reviewed.</summary>
public enum NewsSourceStatus
{
    Blocked = 0,
    Allowed = 1,
}
