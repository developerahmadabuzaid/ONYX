using System.Text.Json;
using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Contracts.Common;
using Onyx.Contracts.Privacy;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Privacy;

namespace Onyx.Application.Privacy;

/// <summary>
/// Data-subject requests (architecture.md §14). The subject is the email stored on the request when it was received; later
/// steps take only the request id, so no caller can point an operation at someone else's records. A request runs only once it
/// is verified. Export and erasure lock the request row for the length of their transaction, so concurrent executions run one
/// after the other and a repeated erasure finds the request completed and changes nothing. Audit entries carry ids and counts,
/// never the address or exported content.
/// </summary>
public sealed class DataSubjectRequestService(
    IDataSubjectRequestRepository requests,
    IPersonalDataStore personalData,
    IAccountPrivacyStore accounts,
    SubjectEraser eraser,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IDataSubjectRequestService
{
    public const int MaxPageSize = 100;

    private static readonly Error NotFound = Error.NotFound("Privacy.RequestNotFound", "Data-subject request not found.");

    public async Task<Result<PagedResult<DataSubjectRequestResponse>>> ListAsync(DataSubjectRequestListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        DataSubjectRequestStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!EnumParsing.TryParse<DataSubjectRequestStatus>(query.Status, out var parsed))
            {
                return Error.Validation("Privacy.InvalidStatus", "Unknown request status.");
            }

            status = parsed;
        }

        var page = await requests.ListAsync(status, Math.Max(query.Page, 1), Math.Clamp(query.PageSize, 1, MaxPageSize), cancellationToken).ConfigureAwait(false);
        return new PagedResult<DataSubjectRequestResponse>(page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<DataSubjectRequestResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await requests.GetAsync(id, cancellationToken).ConfigureAwait(false) is { } request ? ToResponse(request) : NotFound;

    public async Task<Result<DataSubjectRequestResponse>> CreateAsync(CreateDataSubjectRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!EnumParsing.TryParse<DataSubjectRequestType>(request.Type, out var type))
        {
            return Error.Validation("Privacy.InvalidType", "The request type is Export or Erasure.");
        }

        var received = DataSubjectRequest.Receive(type, request.SubjectEmail, request.Note, timeProvider.GetUtcNow());
        if (received.IsFailure)
        {
            return received.Error!;
        }

        requests.Add(received.Value);
        auditTrail.Record(AuditAction.DataSubjectRequestReceived, AuditCategory.Privacy, nameof(DataSubjectRequest), received.Value.Id.ToString(), new { type = type.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(received.Value);
    }

    public async Task<Result<DataSubjectRequestResponse>> VerifyAsync(Guid id, VerifyDataSubjectRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ChangeAsync(id, request.Version, AuditAction.DataSubjectRequestVerified,
            (subject, actor, now) => subject.Verify(actor, request.VerificationNote, now), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<DataSubjectRequestResponse>> RejectAsync(Guid id, RejectDataSubjectRequestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ChangeAsync(id, request.Version, AuditAction.DataSubjectRequestRejected,
            (subject, actor, now) => subject.Reject(actor, request.Reason, now), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Generates the export on demand (never stored) and completes the request on the first successful generation.</summary>
    public Task<Result<DataSubjectExportFile>> ExportAsync(Guid id, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteInTransactionAsync<DataSubjectExportFile>(async token =>
        {
            var actor = Actor();
            var request = await requests.LockAsync(id, token).ConfigureAwait(false);
            if (request is null)
            {
                return NotFound;
            }

            var executable = request.EnsureExecutable(DataSubjectRequestType.Export);
            if (executable.IsFailure)
            {
                return executable.Error!;
            }

            var email = request.SubjectEmail!;
            var now = timeProvider.GetUtcNow();
            var records = await personalData.LoadSubjectAsync(email, token).ConfigureAwait(false);
            var emails = await personalData.ListEmailsToAsync(email, token).ConfigureAwait(false);
            var account = await accounts.ExportAsync(email, token).ConfigureAwait(false);
            var content = DataSubjectExportBuilder.Build(request, email, records, emails, account, now);

            var counts = new
            {
                leads = records.Leads.Count,
                inquiries = records.Inquiries.Count,
                bookings = records.Bookings.Count,
                notes = records.Notes.Count,
                emails = emails.Count,
                account = account is not null,
            };
            request.Complete(actor, JsonSerializer.Serialize(counts), now);
            auditTrail.Record(AuditAction.DataSubjectDataExported, AuditCategory.Privacy, nameof(DataSubjectRequest), request.Id.ToString(), counts);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return new DataSubjectExportFile(DataSubjectExportBuilder.FileName(request), content);
        }, cancellationToken);

    /// <summary>
    /// Erases the subject in one transaction: the admin account (if any), every lead record with the address, emails to the
    /// address, and the address on every request about them. The operator must type the address again. Re-running a completed
    /// erasure returns the completed request without changing anything.
    /// </summary>
    public Task<Result<DataSubjectRequestResponse>> EraseAsync(Guid id, ConfirmErasureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return unitOfWork.ExecuteInTransactionAsync<DataSubjectRequestResponse>(async token =>
        {
            var actor = Actor();
            var subject = await requests.LockAsync(id, token).ConfigureAwait(false);
            if (subject is null)
            {
                return NotFound;
            }

            if (subject is { Type: DataSubjectRequestType.Erasure, Status: DataSubjectRequestStatus.Completed })
            {
                return ToResponse(subject);
            }

            var executable = subject.EnsureExecutable(DataSubjectRequestType.Erasure);
            if (executable.IsFailure)
            {
                return executable.Error!;
            }

            var email = subject.SubjectEmail!;
            if (!string.Equals(request.ConfirmSubjectEmail?.Trim(), email, StringComparison.OrdinalIgnoreCase))
            {
                return Error.Validation("Privacy.ConfirmationMismatch", "Type the subject's email address exactly to confirm the erasure.");
            }

            var now = timeProvider.GetUtcNow();
            var account = await accounts.EraseAsync(email, actor, now, token).ConfigureAwait(false);
            if (account.IsFailure)
            {
                return account.Error!;
            }

            var records = await personalData.LoadSubjectAsync(email, token).ConfigureAwait(false);
            var counts = await eraser.EraseAsync(records, actor, now, token).ConfigureAwait(false);
            var emails = await personalData.DeleteEmailsToAsync(email, token).ConfigureAwait(false);

            var related = await requests.ListForSubjectAsync(subject.SubjectEmailHash, token).ConfigureAwait(false);
            foreach (var other in related.Append(subject).DistinctBy(item => item.Id))
            {
                other.ForgetSubject();
            }

            counts = counts with { EmailsDeleted = emails, AccountErased = account.Value is not null, RequestsForgotten = related.Count };
            subject.Complete(actor, JsonSerializer.Serialize(counts), now);
            auditTrail.Record(AuditAction.DataSubjectErased, AuditCategory.Privacy, nameof(DataSubjectRequest), subject.Id.ToString(), counts);
            await unitOfWork.SaveChangesAsync(token).ConfigureAwait(false);
            return ToResponse(subject);
        }, cancellationToken);
    }

    private async Task<Result<DataSubjectRequestResponse>> ChangeAsync(
        Guid id, uint? version, AuditAction action, Func<DataSubjectRequest, Guid, DateTimeOffset, Result> change, CancellationToken cancellationToken)
    {
        var request = await requests.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return NotFound;
        }

        if (version is { } expected)
        {
            requests.SetExpectedVersion(request, expected);
        }

        var changed = change(request, Actor(), timeProvider.GetUtcNow());
        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        auditTrail.Record(action, AuditCategory.Privacy, nameof(DataSubjectRequest), request.Id.ToString(), new { status = request.Status.ToString() });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(request);
    }

    private Guid Actor() => currentUser.UserId ?? throw new InvalidOperationException("Privacy operations require an authenticated administrator.");

    private static DataSubjectRequestResponse ToResponse(DataSubjectRequest request) =>
        new(
            request.Id, request.Type.ToString(), request.Status.ToString(), request.SubjectEmail, request.SubjectEmailHash[..12], request.RequestNote,
            request.ReceivedAtUtc, request.VerifiedAtUtc, request.VerificationNote, request.CompletedAtUtc, request.ResultSummary, request.RejectionReason,
            request.Version);
}
