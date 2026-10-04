namespace Onyx.Domain.Content;

/// <summary>Stored as text; append only.</summary>
public enum RelationKind
{
    Related = 0,

    /// <summary>A service relevant to an industry (or the reverse).</summary>
    ServiceIndustry = 1,
}
