namespace Onyx.Domain.Intelligence;

/// <summary>How well-founded a recommendation or proposed date is. Never implies data that does not exist.</summary>
public enum RecommendationBasis
{
    /// <summary>Backed by recorded evidence items.</summary>
    Evidence = 0,

    /// <summary>A reasoned estimate without supporting data; must be labelled as such.</summary>
    Estimated = 1,

    /// <summary>Not enough real data to recommend anything yet.</summary>
    InsufficientData = 2,

    /// <summary>An editor's own decision.</summary>
    Manual = 3,
}
