namespace Onyx.Domain.Intelligence;

/// <summary>Where a piece of evidence comes from. Stored as text; append only.</summary>
public enum EvidenceSource
{
    /// <summary>Onyx's own deterministic rules over site content (e.g. orphan page, broken link).</summary>
    RuleEngine = 0,

    /// <summary>Facts observed in Onyx's own content (e.g. two pages target the same keyword).</summary>
    InternalSite = 1,

    ApprovedKnowledge = 2,

    /// <summary>An editor's judgement, recorded as such.</summary>
    Editor = 3,

    // Connected data providers. Only these may carry quantitative metrics, and only when written by their integrations.
    SearchConsole = 10,
    WebAnalytics = 11,
    OnyxSeoPlatform = 12,
    KeywordProvider = 13,
    SerpResearch = 14,
    BehaviorAnalytics = 15,
}
