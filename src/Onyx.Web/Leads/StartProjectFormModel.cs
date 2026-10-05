namespace Onyx.Web.Leads;

/// <summary>
/// The three-step project brief: areas of interest, the project, then contact details. Each step is validated on the server
/// before the next is shown; earlier answers travel in hidden fields.
/// </summary>
public sealed class StartProjectFormModel : InquiryFormModel
{
    public const int LastStep = 3;

    /// <summary>Answer version stored with the inquiry; bump when the questions change meaning.</summary>
    public const int AnswersVersion = 1;

    /// <summary>The five Onyx pillars (CLAUDE.md §1). Values are stable keys; labels come from resources.</summary>
    public static IReadOnlyList<string> PillarOptions { get; } = ["software", "ai", "automation", "cybersecurity", "digital-growth"];

    public static IReadOnlyList<string> OrganizationSizeOptions { get; } = ["sme", "mid-market", "enterprise"];

    public static IReadOnlyList<string> TimelineOptions { get; } = ["asap", "1-3-months", "3-6-months", "exploring"];

    public int Step { get; set; } = 1;

    /// <summary><c>next</c>, <c>back</c> or <c>submit</c>.</summary>
    public string? Command { get; set; }

    public List<string> Pillars { get; set; } = [];

    public string? OrganizationSize { get; set; }

    public string? Timeline { get; set; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Answers()
    {
        var answers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var pillars = Pillars.Where(PillarOptions.Contains).Distinct(StringComparer.Ordinal).ToList();
        if (pillars.Count > 0)
        {
            answers["pillars"] = pillars;
        }

        if (OrganizationSize is { } size && OrganizationSizeOptions.Contains(size))
        {
            answers["organization-size"] = [size];
        }

        if (Timeline is { } timeline && TimelineOptions.Contains(timeline))
        {
            answers["timeline"] = [timeline];
        }

        return answers;
    }
}
