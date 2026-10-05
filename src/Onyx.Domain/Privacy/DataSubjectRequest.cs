using System.Security.Cryptography;
using System.Text;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Leads;

namespace Onyx.Domain.Privacy;

/// <summary>
/// A data-subject request (architecture.md §14), operated by staff holding <c>Privacy.Manage</c>. The subject is identified only
/// by the email address stored here, never by anything sent with a later step. A request is carried out only after a staff
/// member records how the requester's identity was verified. After an erasure, the plain address is removed from every request
/// for that subject and only its SHA-256 hash remains, so the request still proves what was done without keeping the address.
/// </summary>
public sealed class DataSubjectRequest : AuditableEntity, IAuditable
{
    public const int NoteMaxLength = 1_000;
    public const int SummaryMaxLength = 2_000;
    public const int HashLength = 64;

    private DataSubjectRequest()
    {
        SubjectEmailHash = string.Empty;
    }

    public DataSubjectRequestType Type { get; private set; }

    public DataSubjectRequestStatus Status { get; private set; }

    /// <summary>Normalized address; null once the subject has been erased.</summary>
    [SensitiveData]
    public string? SubjectEmail { get; private set; }

    /// <summary>SHA-256 (hex) of the normalized address; links requests for the same subject without the address.</summary>
    public string SubjectEmailHash { get; private set; }

    /// <summary>How and when the request arrived (free text; may name the requester).</summary>
    [SensitiveData]
    public string? RequestNote { get; private set; }

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    public Guid? VerifiedById { get; private set; }

    public DateTimeOffset? VerifiedAtUtc { get; private set; }

    /// <summary>How the requester's identity was verified (free text).</summary>
    [SensitiveData]
    public string? VerificationNote { get; private set; }

    public Guid? CompletedById { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>Counts of what was exported or erased; never personal data.</summary>
    public string? ResultSummary { get; private set; }

    [SensitiveData]
    public string? RejectionReason { get; private set; }

    public uint Version { get; private set; }

    public AuditCategory AuditCategory => AuditCategory.Privacy;

    public bool IsOpen => Status is DataSubjectRequestStatus.Received or DataSubjectRequestStatus.Verified;

    public static Result<DataSubjectRequest> Receive(DataSubjectRequestType type, string? subjectEmail, string? note, DateTimeOffset now)
    {
        if (!Enum.IsDefined(type))
        {
            return Error.Validation("Privacy.InvalidType", "The request type is Export or Erasure.");
        }

        var contact = ContactDetails.Create("subject", subjectEmail, null, null, null);
        if (contact.IsFailure)
        {
            return Error.Validation("Privacy.InvalidEmail", "Enter the subject's email address.");
        }

        var cleanNote = Clean(note);
        if (cleanNote is { Length: > NoteMaxLength })
        {
            return Error.Validation("Privacy.NoteTooLong", $"Notes are limited to {NoteMaxLength} characters.");
        }

        var email = contact.Value.NormalizedEmail;
        return new DataSubjectRequest
        {
            Type = type,
            Status = DataSubjectRequestStatus.Received,
            SubjectEmail = email,
            SubjectEmailHash = HashEmail(email),
            RequestNote = cleanNote,
            ReceivedAtUtc = now,
        };
    }

    public static string HashEmail(string normalizedEmail) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail.Trim().ToLowerInvariant())));

    public Result Verify(Guid verifiedById, string? verificationNote, DateTimeOffset now)
    {
        if (Status != DataSubjectRequestStatus.Received)
        {
            return Error.Conflict("Privacy.NotReceived", "Only a newly received request can be verified.");
        }

        var note = Clean(verificationNote);
        if (note is null || note.Length > NoteMaxLength)
        {
            return Error.Validation("Privacy.VerificationRequired", "Record how the requester's identity was verified.");
        }

        Status = DataSubjectRequestStatus.Verified;
        VerifiedById = verifiedById;
        VerifiedAtUtc = now;
        VerificationNote = note;
        return Result.Success();
    }

    public Result Reject(Guid actorId, string? reason, DateTimeOffset now)
    {
        if (!IsOpen)
        {
            return Error.Conflict("Privacy.Closed", "This request is already closed.");
        }

        var clean = Clean(reason);
        if (clean is null || clean.Length > NoteMaxLength)
        {
            return Error.Validation("Privacy.ReasonRequired", "Record why the request is rejected.");
        }

        Status = DataSubjectRequestStatus.Rejected;
        RejectionReason = clean;
        CompletedById = actorId;
        CompletedAtUtc = now;
        return Result.Success();
    }

    /// <summary>The verified request may be carried out (an export may be downloaded again once completed).</summary>
    public Result EnsureExecutable(DataSubjectRequestType expectedType)
    {
        if (Type != expectedType)
        {
            return Error.Conflict("Privacy.WrongType", $"This is not an {expectedType} request.");
        }

        if (SubjectEmail is null)
        {
            return Error.Conflict("Privacy.SubjectErased", "The subject of this request has been erased.");
        }

        var executable = Status == DataSubjectRequestStatus.Verified
            || (Status == DataSubjectRequestStatus.Completed && Type == DataSubjectRequestType.Export);
        return executable ? Result.Success() : Error.Conflict("Privacy.NotVerified", "Only a verified request can be carried out.");
    }

    public void Complete(Guid actorId, string summary, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (Status == DataSubjectRequestStatus.Completed)
        {
            return;
        }

        Status = DataSubjectRequestStatus.Completed;
        CompletedById = actorId;
        CompletedAtUtc = now;
        ResultSummary = summary.Length > SummaryMaxLength ? summary[..SummaryMaxLength] : summary;
    }

    /// <summary>Removes the plain address (and free-text notes that may name the subject) once the subject is erased.</summary>
    public void ForgetSubject()
    {
        SubjectEmail = null;
        RequestNote = null;
        VerificationNote = VerificationNote is null ? null : "[erased]";
        RejectionReason = RejectionReason is null ? null : "[erased]";
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
