namespace Onyx.Domain.Proof;

public enum ProofItemKind
{
    /// <summary>A capability the delivered system really has.</summary>
    Capability = 0,

    /// <summary>A measured result with its value and the date it was true.</summary>
    Metric = 1,

    /// <summary>A client's own words, attributed with the client's permission.</summary>
    Testimonial = 2,
}
