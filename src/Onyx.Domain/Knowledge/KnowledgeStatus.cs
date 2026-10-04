namespace Onyx.Domain.Knowledge;

public enum KnowledgeStatus
{
    /// <summary>Proposed; never used to ground generated content.</summary>
    Draft = 0,

    /// <summary>Verified by an authorized person, with a recorded source. The only state AI may rely on.</summary>
    Approved = 1,

    /// <summary>No longer true or no longer to be used.</summary>
    Retired = 2,
}
