namespace Onyx.Domain.Intelligence;

public enum RecommendationStatus
{
    Proposed = 0,
    Approved = 1,
    Rejected = 2,

    /// <summary>The approved change was applied to the working copy (it still goes through review before it is public).</summary>
    Applied = 3,

    Superseded = 4,
}
