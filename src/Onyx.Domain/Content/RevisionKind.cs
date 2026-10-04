namespace Onyx.Domain.Content;

/// <summary>Why a revision was recorded. Stored as text; append only.</summary>
public enum RevisionKind
{
    Created = 0,
    Edited = 1,
    SeoUpdated = 2,
    SettingsUpdated = 3,
    Restored = 4,
    RecommendationApplied = 5,
}
