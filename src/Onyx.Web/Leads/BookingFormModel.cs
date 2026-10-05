namespace Onyx.Web.Leads;

/// <summary>
/// The consultation form. With online booking available the visitor picks a slot (<see cref="StartUtc"/>); otherwise it is a
/// consultation request (<see cref="RequestOnly"/>), which needs a short message instead.
/// </summary>
public sealed class BookingFormModel : InquiryFormModel
{
    public DateTimeOffset? StartUtc { get; set; }

    public bool RequestOnly { get; set; }

    public List<string> Pillars { get; set; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Answers()
    {
        var pillars = Pillars.Where(StartProjectFormModel.PillarOptions.Contains).Distinct(StringComparer.Ordinal).ToList();
        return pillars.Count > 0
            ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) { ["pillars"] = pillars }
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
    }
}
