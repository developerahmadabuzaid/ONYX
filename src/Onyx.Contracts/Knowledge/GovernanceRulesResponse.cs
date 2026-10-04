using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Knowledge;

public sealed record GovernanceRulesResponse(IReadOnlyList<string> Rules);
