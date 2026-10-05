namespace Onyx.Contracts.Privacy;

/// <summary>The subject's email typed again by the operator; erasure runs only when it matches the stored request.</summary>
public sealed record ConfirmErasureRequest(string ConfirmSubjectEmail);
