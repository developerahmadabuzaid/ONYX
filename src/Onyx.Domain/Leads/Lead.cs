using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Leads;

/// <summary>
/// The sales pipeline record built from one or more inquiries (architecture.md §13.2): stage, source, pillar, owner,
/// priority, a transparent rule-based score and an activity timeline. Inquiries from the same email are attached to the
/// open lead instead of creating duplicates.
/// </summary>
public sealed class Lead : AuditableEntity, IAuditable
{
    private readonly List<LeadActivity> _pendingActivities = [];

    private Lead()
    {
        DisplayName = string.Empty;
        NormalizedEmail = string.Empty;
        ScoreReasons = [];
    }

    [SensitiveData]
    public string DisplayName { get; private set; }

    [SensitiveData]
    public string NormalizedEmail { get; private set; }

    public string? Company { get; private set; }

    public LeadStage Stage { get; private set; }

    /// <summary>The type of the inquiry that created the lead.</summary>
    public InquiryType Source { get; private set; }

    public Guid? PillarId { get; private set; }

    public Guid? OwnerId { get; private set; }

    public LeadPriority Priority { get; private set; }

    public int Score { get; private set; }

    public IReadOnlyList<string> ScoreReasons { get; private set; }

    public string LanguageCode { get; private set; } = string.Empty;

    public int InquiryCount { get; private set; }

    public DateTimeOffset FirstInquiryAtUtc { get; private set; }

    public DateTimeOffset LastInquiryAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool IsOpen => Stage is not (LeadStage.Won or LeadStage.Lost);

    public AuditCategory AuditCategory => AuditCategory.Leads;

    /// <summary>Activities created by the last behaviour calls, for the repository to add with the lead.</summary>
    public IReadOnlyList<LeadActivity> PendingActivities => _pendingActivities;

    public static Lead Open(ContactDetails contact, InquiryType source, Guid? pillarId, string languageCode, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(contact);
        return new Lead
        {
            DisplayName = contact.Name,
            NormalizedEmail = contact.NormalizedEmail,
            Company = contact.Company,
            Stage = LeadStage.New,
            Source = source,
            PillarId = pillarId,
            Priority = LeadPriority.Normal,
            LanguageCode = languageCode,
            FirstInquiryAtUtc = now,
            LastInquiryAtUtc = now,
        };
    }

    /// <summary>Attaches a stored inquiry: refreshes the contact summary, the score and the timeline.</summary>
    public void AttachInquiry(Inquiry inquiry, IEnumerable<Inquiry> earlierInquiries, Guid? pillarId)
    {
        ArgumentNullException.ThrowIfNull(inquiry);
        DisplayName = inquiry.Name;
        Company = inquiry.Company ?? Company;
        PillarId ??= pillarId;
        LanguageCode = inquiry.LanguageCode;
        InquiryCount++;
        LastInquiryAtUtc = inquiry.SubmittedAtUtc;
        (Score, ScoreReasons) = LeadScore.Evaluate(earlierInquiries.Append(inquiry));
        Record(LeadActivityKind.InquiryReceived, inquiry.Type.ToString(), inquiry.Id, null, inquiry.SubmittedAtUtc);
    }

    public Result ChangeStage(LeadStage stage, Guid actorId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(stage))
        {
            return Error.Validation("Lead.InvalidStage", "Unknown pipeline stage.");
        }

        if (stage == Stage)
        {
            return Result.Success();
        }

        var previous = Stage;
        Stage = stage;
        ClosedAtUtc = IsOpen ? null : now;
        Record(LeadActivityKind.StageChanged, $"{previous}->{stage}", null, actorId, now);
        return Result.Success();
    }

    public void AssignTo(Guid? ownerId, Guid actorId, DateTimeOffset now)
    {
        if (ownerId == OwnerId)
        {
            return;
        }

        OwnerId = ownerId;
        Record(LeadActivityKind.Assigned, null, ownerId, actorId, now);
    }

    public Result ChangePriority(LeadPriority priority, Guid actorId, DateTimeOffset now)
    {
        if (!Enum.IsDefined(priority))
        {
            return Error.Validation("Lead.InvalidPriority", "Unknown priority.");
        }

        if (priority != Priority)
        {
            Priority = priority;
            Record(LeadActivityKind.PriorityChanged, priority.ToString(), null, actorId, now);
        }

        return Result.Success();
    }

    public void RecordActivity(LeadActivityKind kind, string? detail, Guid? relatedId, Guid? actorId, DateTimeOffset now) =>
        Record(kind, detail, relatedId, actorId, now);

    public void ClearPendingActivities() => _pendingActivities.Clear();

    private void Record(LeadActivityKind kind, string? detail, Guid? relatedId, Guid? actorId, DateTimeOffset now) =>
        _pendingActivities.Add(LeadActivity.Record(Id, kind, detail, relatedId, actorId, now));
}
