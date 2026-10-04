using Onyx.Application.Knowledge;

namespace Onyx.Application.Intelligence;

/// <summary>
/// Input for a future LLM provider. Generation is grounded in <see cref="GroundingFacts"/> (approved knowledge only) and
/// bound by <see cref="GovernanceRules"/>. No secrets or lead PII may appear in any field.
/// </summary>
public sealed record TextGenerationRequest(
    string Purpose,
    string LanguageCode,
    string Instructions,
    IReadOnlyList<GroundingFact> GroundingFacts,
    IReadOnlyList<string> GovernanceRules,
    int MaxOutputTokens);
