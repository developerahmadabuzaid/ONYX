namespace Onyx.Domain.Privacy;

public enum RetentionAction
{
    /// <summary>Remove the records.</summary>
    Delete = 0,

    /// <summary>Keep the records but remove their personal fields (same rules as a data-subject erasure).</summary>
    Anonymize = 1,
}
