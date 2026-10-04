using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Knowledge;

/// <summary><see cref="Outcome"/>: Answered (with the approved knowledge entry) or Dismissed.</summary>
public sealed record ResolveInformationRequest(string Outcome, Guid? KnowledgeEntryId, string? Note);
