using Onyx.Domain.Knowledge;

namespace Onyx.Application.Knowledge;

/// <summary>
/// The only source of facts about Onyx that future AI writing may use: approved entries in the requested language, plus the
/// code-owned governance rules. Draft and retired entries are never included.
/// </summary>
public sealed class CompanyKnowledgeSource(IKnowledgeRepository knowledge)
{
    public async Task<IReadOnlyList<GroundingFact>> GetApprovedFactsAsync(string languageCode, CancellationToken cancellationToken)
    {
        var approved = await knowledge.ListAsync(null, KnowledgeStatus.Approved, null, cancellationToken).ConfigureAwait(false);
        return approved
            .Select(entry => (entry, statement: entry.Statements.FirstOrDefault(text => text.LanguageCode == languageCode)))
            .Where(pair => pair.statement is not null)
            .Select(pair => new GroundingFact(pair.entry.Id, pair.entry.Category.ToString(), pair.entry.Title, pair.statement!.Text))
            .ToList();
    }

    public static IReadOnlyList<string> GovernanceRules => ContentGovernanceRules.All;
}
