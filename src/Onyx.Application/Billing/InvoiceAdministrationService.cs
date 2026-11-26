using Onyx.Application.Abstractions.Persistence;
using Onyx.Application.Auditing;
using Onyx.Application.Commerce;
using Onyx.Application.Customers;
using Onyx.Application.Subscriptions;
using Onyx.Contracts.Commerce;
using Onyx.Contracts.Common;
using Onyx.Domain.Auditing;
using Onyx.Domain.Billing;
using Onyx.Domain.Common;
using Onyx.Domain.Customers;

namespace Onyx.Application.Billing;

/// <summary>
/// Manual invoicing by staff until a payment provider is approved: draft, issue (numbered, bill-to snapshot), mark paid, void.
/// Amounts come only from what staff enter.
/// </summary>
public sealed class InvoiceAdministrationService(
    IInvoiceRepository invoices,
    ICustomerAccountRepository accounts,
    ICustomerSubscriptionRepository subscriptions,
    ICommerceReadModel readModel,
    IAuditTrail auditTrail,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<PagedResult<AdminInvoiceListItemResponse>> ListAsync(string? status, Guid? customerId, int page, int pageSize, CancellationToken cancellationToken) =>
        readModel.AdminInvoicesAsync(status, customerId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public async Task<Result<AdminInvoiceResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await readModel.AdminInvoiceAsync(id, cancellationToken).ConfigureAwait(false) is { } invoice ? invoice : CommerceErrors.InvoiceNotFound;

    public async Task<Result<AdminInvoiceResponse>> CreateAsync(SaveInvoiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var checkedParties = await CheckPartiesAsync(request, cancellationToken).ConfigureAwait(false);
        if (checkedParties.IsFailure)
        {
            return checkedParties.Error!;
        }

        var invoice = Invoice.Draft(request.CustomerId, request.SubscriptionId, request.Currency, request.DueOn, request.Notes);
        if (invoice.IsFailure)
        {
            return invoice.Error!;
        }

        var lines = invoice.Value.ReplaceLines(Lines(request));
        if (lines.IsFailure)
        {
            return lines.Error!;
        }

        invoices.Add(invoice.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(invoice.Value.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminInvoiceResponse>> UpdateAsync(Guid id, SaveInvoiceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is not { } version)
        {
            return CommerceErrors.VersionRequired;
        }

        var invoice = await invoices.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return CommerceErrors.InvoiceNotFound;
        }

        if (invoice.CustomerId != request.CustomerId)
        {
            return Error.Validation("Invoice.CustomerFixed", "The customer of an invoice cannot change.");
        }

        invoices.SetExpectedVersion(invoice, version);
        var updated = invoice.UpdateDraft(request.Currency, request.DueOn, request.Notes);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        var lines = invoice.ReplaceLines(Lines(request));
        if (lines.IsFailure)
        {
            return lines.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<AdminInvoiceResponse>> CommandAsync(Guid id, string command, InvoiceCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoice = await invoices.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return CommerceErrors.InvoiceNotFound;
        }

        invoices.SetExpectedVersion(invoice, request.Version);
        var now = timeProvider.GetUtcNow();
        Result outcome;
        var action = AuditAction.InvoiceIssued;
        switch (command)
        {
            case "issue":
                var account = await accounts.GetAsync(invoice.CustomerId, cancellationToken).ConfigureAwait(false);
                if (account is null || account.Status == CustomerAccountStatus.Erased)
                {
                    return Error.Conflict("Invoice.CustomerUnavailable", "The customer account is no longer available.");
                }

                if (invoice.Status != InvoiceStatus.Draft || invoice.Total <= 0)
                {
                    // The domain explains why (not a draft, or empty); no number is consumed for a refused issue.
                    outcome = invoice.Issue("pending", string.Empty, string.Empty, now);
                    break;
                }

                var number = await invoices.NextNumberAsync(now, cancellationToken).ConfigureAwait(false);
                outcome = invoice.Issue(number, account.Company ?? account.DisplayName, account.Email, now);
                break;
            case "paid":
                outcome = invoice.MarkPaid(now);
                action = AuditAction.InvoicePaid;
                break;
            case "void":
                outcome = invoice.Void(now);
                action = AuditAction.InvoiceVoided;
                break;
            default:
                return Error.NotFound("Invoice.UnknownCommand", "Unknown command.");
        }

        if (outcome.IsFailure)
        {
            return outcome.Error!;
        }

        auditTrail.Record(action, AuditCategory.Commerce, nameof(Invoice), id.ToString(), new { number = invoice.Number, total = invoice.Total, currency = invoice.Currency });
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> CheckPartiesAsync(SaveInvoiceRequest request, CancellationToken cancellationToken)
    {
        var account = await accounts.GetAsync(request.CustomerId, cancellationToken).ConfigureAwait(false);
        if (account is null || account.Status == CustomerAccountStatus.Erased)
        {
            return CommerceErrors.CustomerNotFound;
        }

        if (request.SubscriptionId is not { } subscriptionId)
        {
            return Result.Success();
        }

        var subscription = await subscriptions.GetAsync(subscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            return CommerceErrors.SubscriptionNotFound;
        }

        return subscription.CustomerId == request.CustomerId
            ? Result.Success()
            : Error.Validation("Invoice.SubscriptionMismatch", "The subscription belongs to another customer.");
    }

    private static List<(string? Description, decimal Quantity, decimal UnitPrice)> Lines(SaveInvoiceRequest request) =>
        (request.Lines ?? []).Select(line => ((string?)line.Description, line.Quantity, line.UnitPrice)).ToList();
}
