using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record IntelligenceCapabilitiesResponse(IReadOnlyList<DataSourceStatusResponse> Sources, IReadOnlyList<string> GovernanceRules);
