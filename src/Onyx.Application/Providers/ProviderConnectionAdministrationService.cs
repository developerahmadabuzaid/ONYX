using Onyx.Application.Abstractions;
using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Email;
using Onyx.Application.Identity;
using Onyx.Contracts.Administration;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Providers;

namespace Onyx.Application.Providers;

public sealed class ProviderConnectionAdministrationService(
    IProviderConnectionRepository repository,
    IEmailOutbox emailOutbox,
    IUserDirectory userDirectory,
    ICurrentUser currentUser,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork) : IProviderConnectionAdministrationService
{
    public const string ProviderTestTemplateKey = "system.provider-test";

    private static readonly Error NotFound = Error.NotFound("ProviderConnection.NotFound", "Provider connection not found.");

    public async Task<IReadOnlyList<ProviderConnectionResponse>> ListAsync(CancellationToken cancellationToken) =>
        (await repository.ListAsync(cancellationToken).ConfigureAwait(false)).Select(ToResponse).ToList();

    public Task<Result<ProviderConnectionResponse>> DisableAsync(string providerKey, CancellationToken cancellationToken) =>
        ChangeAsync(providerKey, connection => connection.Disable(), AuditAction.ProviderConnectionDisabled, cancellationToken);

    public Task<Result<ProviderConnectionResponse>> EnableAsync(string providerKey, CancellationToken cancellationToken) =>
        ChangeAsync(providerKey, connection => connection.Enable(), AuditAction.ProviderConnectionEnabled, cancellationToken);

    public async Task<Result<Guid>> QueueEmailTestAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new InvalidOperationException("An authenticated admin is required.");
        var contact = await userDirectory.GetContactAsync(userId, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return Error.NotFound("User.NotFound", "Current user not found.");
        }

        var queued = await emailOutbox.QueueAsync(
            new EmailRequest(ProviderTestTemplateKey, contact.PreferredLanguage, contact.Email, contact.DisplayName, new Dictionary<string, string>()),
            cancellationToken).ConfigureAwait(false);
        if (queued.IsFailure)
        {
            return queued;
        }

        auditTrail.Record(AuditAction.ProviderTestRequested, AuditCategory.Integrations, "EmailMessage", queued.Value.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return queued.Value;
    }

    private async Task<Result<ProviderConnectionResponse>> ChangeAsync(
        string providerKey, Action<ProviderConnection> change, AuditAction action, CancellationToken cancellationToken)
    {
        var connection = await repository.GetByKeyAsync(providerKey, cancellationToken).ConfigureAwait(false);
        if (connection is null)
        {
            return NotFound;
        }

        change(connection);
        auditTrail.Record(action, AuditCategory.Integrations, nameof(ProviderConnection), connection.Id.ToString(), new { providerKey });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToResponse(connection);
    }

    private static ProviderConnectionResponse ToResponse(ProviderConnection connection) =>
        new(connection.ProviderKey, connection.Category.ToString(), connection.Status.ToString(),
            connection.LastCheckedAtUtc, connection.LastSucceededAtUtc, connection.LastError);
}
