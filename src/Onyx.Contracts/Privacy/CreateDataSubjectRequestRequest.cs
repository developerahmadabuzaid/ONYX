namespace Onyx.Contracts.Privacy;

/// <summary><see cref="Type"/> is Export or Erasure; the subject is identified only by <see cref="SubjectEmail"/>.</summary>
public sealed record CreateDataSubjectRequestRequest(string Type, string SubjectEmail, string? Note);
