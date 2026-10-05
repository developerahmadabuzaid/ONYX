namespace Onyx.Domain.Privacy;

public enum DataSubjectRequestType
{
    /// <summary>A machine-readable copy of the subject's personal data.</summary>
    Export = 0,

    /// <summary>Deletion of the subject's personal data; records kept for integrity keep only non-personal fields.</summary>
    Erasure = 1,
}
