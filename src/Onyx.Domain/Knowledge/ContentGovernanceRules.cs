namespace Onyx.Domain.Knowledge;

/// <summary>
/// Non-negotiable writing rules for any generated or assisted Onyx content (CLAUDE.md §2). Code-owned, not editable: future
/// AI prompts and the editor checklist include them verbatim. When a claim would need a fact that approved knowledge does not
/// contain, the workflow raises an <see cref="InformationRequest"/> instead.
/// </summary>
public static class ContentGovernanceRules
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Use only Approved Company Knowledge for any statement about Onyx.",
        "Never invent clients, client names, project names or logos.",
        "Never invent metrics, statistics, outcomes or project results.",
        "Never invent testimonials or reviews.",
        "Never invent certifications, awards or partnerships.",
        "Never invent prices, packages or 'starting from' amounts.",
        "Never invent technologies, integrations or product capabilities.",
        "Never invent years of experience, team size or offices.",
        "Never promise or guarantee business results; never claim anything is 'unhackable' or 'fully secure'.",
        "Never fabricate search volume, keyword difficulty, rankings, traffic, CTR or competitor metrics.",
        "Never name or compare competitors in public content without explicit owner approval.",
        "When a required fact is missing, mark the content as needing information instead of filling the gap.",
    ];
}
