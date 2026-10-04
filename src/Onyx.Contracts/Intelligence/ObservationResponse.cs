using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary><see cref="Assessment"/> describes association only: InsufficientData, AssociatedWithImprovement, NoClearChange, AssociatedWithDecline.</summary>
public sealed record ObservationResponse(Guid Id, string Kind, DateTimeOffset RecordedAtUtc, EvidenceDto Evidence, string Assessment, string? Note);
