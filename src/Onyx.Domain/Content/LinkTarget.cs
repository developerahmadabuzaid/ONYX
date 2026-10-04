namespace Onyx.Domain.Content;

/// <summary>
/// Exactly one of: an internal content item (rendered as its current URL, so slug changes never break links), a
/// site-relative path, or an absolute https URL.
/// </summary>
public sealed record LinkTarget(Guid? ContentItemId = null, string? Path = null, string? Url = null)
{
    public bool IsInternal => ContentItemId is not null || Path is not null;
}
