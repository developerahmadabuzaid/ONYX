using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Commerce;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Customers;

namespace Onyx.Application.Customers;

/// <summary>
/// Staff view of customer accounts. Staff can disable or re-enable an account (disabling ends every session immediately) but
/// never see or set a customer's password, and customers never gain staff roles or permissions.
/// </summary>
public sealed class CustomerAdministrationService(
    ICustomerAccountRepository accounts,
    ICommerceReadModel readModel,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork)
{
    public Task<PagedResult<AdminCustomerListItemResponse>> ListAsync(string? search, string? status, int page, int pageSize, CancellationToken cancellationToken) =>
        readModel.AdminCustomersAsync(search?.Trim(), status, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public async Task<Result<AdminCustomerResponse>> GetAsync(Guid id, string language, CancellationToken cancellationToken) =>
        await readModel.AdminCustomerAsync(id, CommerceErrors.Language(language), cancellationToken).ConfigureAwait(false) is { } customer
            ? customer
            : CommerceErrors.CustomerNotFound;

    public async Task<Result<AdminCustomerResponse>> SetStatusAsync(Guid id, CustomerStatusRequest request, string language, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var account = await accounts.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (account is null || account.Status == CustomerAccountStatus.Erased)
        {
            return CommerceErrors.CustomerNotFound;
        }

        accounts.SetExpectedVersion(account, request.Version);
        var changed = request.Enabled ? account.Enable() : account.Disable();
        if (changed.IsFailure)
        {
            return changed.Error!;
        }

        auditTrail.Record(request.Enabled ? AuditAction.CustomerEnabled : AuditAction.CustomerDisabled, AuditCategory.Customers, nameof(CustomerAccount), id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, language, cancellationToken).ConfigureAwait(false);
    }
}
