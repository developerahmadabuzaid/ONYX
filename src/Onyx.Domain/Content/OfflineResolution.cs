namespace Onyx.Domain.Content;

/// <summary>
/// What happens to the URLs of content that was public and no longer is. Never defaults to the homepage:
/// until an editor decides, the URLs answer 410 Gone and the item is listed as needing a decision.
/// </summary>
public enum OfflineResolution
{
    /// <summary>The item is live, or was never public.</summary>
    None = 0,

    /// <summary>Taken offline without a decision yet: served as 410 Gone and flagged for review.</summary>
    PendingDecision = 1,

    /// <summary>Editor confirmed there is no legitimate replacement: 410 Gone.</summary>
    Gone = 2,

    /// <summary>Editor chose a specific, relevant replacement: 301 to its current URL.</summary>
    RedirectToContent = 3,
}
