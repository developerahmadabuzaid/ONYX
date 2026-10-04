namespace Onyx.Domain.Content;

public enum RedirectKind
{
    /// <summary>301 to an internal target.</summary>
    Permanent = 0,

    /// <summary>410: the URL existed and is intentionally gone (no legitimate replacement).</summary>
    Gone = 1,
}
