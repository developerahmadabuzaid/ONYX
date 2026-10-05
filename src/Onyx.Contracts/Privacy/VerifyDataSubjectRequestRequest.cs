namespace Onyx.Contracts.Privacy;

/// <summary>Records how the requester's identity was verified (required before the request can be carried out).</summary>
public sealed record VerifyDataSubjectRequestRequest(string VerificationNote, uint? Version);
