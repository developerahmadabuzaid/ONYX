using Onyx.Domain.Billing;

namespace Onyx.Application.Billing;

public interface IInvoiceRepository
{
    Task<Invoice?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Next invoice number from a database sequence, e.g. <c>INV-2026-00042</c>; gaps are possible, duplicates are not.</summary>
    Task<string> NextNumberAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);

    void Add(Invoice invoice);

    void SetExpectedVersion(Invoice invoice, uint version);
}
