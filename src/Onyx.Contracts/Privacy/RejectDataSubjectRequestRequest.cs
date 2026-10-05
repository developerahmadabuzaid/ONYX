namespace Onyx.Contracts.Privacy;

public sealed record RejectDataSubjectRequestRequest(string Reason, uint? Version);
