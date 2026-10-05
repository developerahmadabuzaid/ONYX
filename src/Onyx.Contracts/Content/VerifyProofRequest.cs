using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

/// <summary><see cref="ClientPermission"/>: NotApplicable or Granted (Granted needs <see cref="ClientPermissionNote"/>).</summary>
public sealed record VerifyProofRequest(string SourceNote, string ClientPermission, string? ClientPermissionNote, uint Version);
