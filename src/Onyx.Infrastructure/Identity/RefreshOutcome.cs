using Onyx.Contracts.Identity;

namespace Onyx.Infrastructure.Identity;

public sealed record RefreshOutcome(RefreshOutcomeKind Kind, TokenPairResponse? Tokens = null, Guid? UserId = null, Guid? SessionId = null);
