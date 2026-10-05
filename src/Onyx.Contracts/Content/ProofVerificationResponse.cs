using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

public sealed record ProofVerificationResponse(Guid VerifiedById, DateTimeOffset VerifiedAtUtc, string SourceNote, string ClientPermission, string? ClientPermissionNote);
