namespace Onyx.Domain.Leads;

/// <summary>
/// A transparent, rule-based score (architecture.md §13.2): every point comes from a named, visible reason, so staff can see
/// why a lead ranks where it does. It orders work only; it is not a prediction and is never shown to visitors.
/// </summary>
public static class LeadScore
{
    public const int MaxScore = 100;

    public static (int Score, IReadOnlyList<string> Reasons) Evaluate(IEnumerable<Inquiry> inquiries)
    {
        var list = inquiries.ToList();
        var reasons = new List<string>();
        var score = 0;

        void Add(bool condition, int points, string reason)
        {
            if (condition)
            {
                score += points;
                reasons.Add(reason);
            }
        }

        Add(list.Any(inquiry => inquiry.Type == InquiryType.Consultation), 30, "booked-consultation");
        Add(list.Any(inquiry => inquiry.Type is InquiryType.StartProject or InquiryType.RequestConsultation), 20, "project-or-consultation-request");
        Add(list.Any(inquiry => inquiry.Type is InquiryType.ServiceInquiry or InquiryType.SimilarProject), 10, "service-or-project-interest");
        Add(list.Any(inquiry => inquiry.Company is not null), 10, "company-provided");
        Add(list.Any(inquiry => inquiry.Phone is not null), 10, "phone-provided");
        Add(list.Any(inquiry => inquiry.Answers.Values.Count >= 3), 10, "detailed-brief");
        Add(list.Count > 1, 10, "repeat-inquiry");
        return (Math.Min(score, MaxScore), reasons);
    }
}
