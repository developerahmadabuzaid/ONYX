using System.Text.RegularExpressions;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;

namespace Onyx.Domain.Billing;

/// <summary>
/// A customer invoice recorded by staff (manual billing until a payment provider is approved). Draft → Issued → Paid, or Void
/// from Draft or Issued. Lines change only while the invoice is a draft; issuing assigns the number and snapshots the bill-to
/// details, so later profile changes or erasure never alter an issued invoice.
/// </summary>
public sealed partial class Invoice : AuditableEntity, IAuditable
{
    public const int NumberMaxLength = 30;
    public const int NotesMaxLength = 1000;
    public const int MaxLines = 50;

    private readonly List<InvoiceLine> _lines = [];

    private Invoice()
    {
        Currency = string.Empty;
    }

    public Guid CustomerId { get; private set; }

    public Guid? SubscriptionId { get; private set; }

    public string? Number { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public string Currency { get; private set; }

    public decimal Total { get; private set; }

    public DateOnly DueOn { get; private set; }

    public string? Notes { get; private set; }

    [SensitiveData]
    public string? BillToName { get; private set; }

    [SensitiveData]
    public string? BillToEmail { get; private set; }

    public DateTimeOffset? IssuedAtUtc { get; private set; }

    public DateTimeOffset? PaidAtUtc { get; private set; }

    public DateTimeOffset? VoidedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

    public AuditCategory AuditCategory => AuditCategory.Commerce;

    /// <summary>Customers never see drafts.</summary>
    public bool IsVisibleToCustomer => Status != InvoiceStatus.Draft;

    public static Result<Invoice> Draft(Guid customerId, Guid? subscriptionId, string? currency, DateOnly dueOn, string? notes)
    {
        var invoice = new Invoice { CustomerId = customerId, SubscriptionId = subscriptionId, Status = InvoiceStatus.Draft };
        var updated = invoice.UpdateDraft(currency, dueOn, notes);
        return updated.IsFailure ? updated.Error! : invoice;
    }

    public Result UpdateDraft(string? currency, DateOnly dueOn, string? notes)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Error.Conflict("Invoice.NotDraft", "Only a draft invoice can be changed.");
        }

        var cleanCurrency = currency?.Trim().ToUpperInvariant();
        if (cleanCurrency is null || !CurrencyPattern().IsMatch(cleanCurrency))
        {
            return Error.Validation("Invoice.InvalidCurrency", "Use a three-letter currency code (for example SAR, JOD or USD).");
        }

        var cleanNotes = notes?.Trim();
        if (cleanNotes is { Length: > NotesMaxLength })
        {
            return Error.Validation("Invoice.NotesTooLong", $"Notes are at most {NotesMaxLength} characters.");
        }

        Currency = cleanCurrency;
        DueOn = dueOn;
        Notes = string.IsNullOrEmpty(cleanNotes) ? null : cleanNotes;
        return Result.Success();
    }

    public Result ReplaceLines(IReadOnlyList<(string? Description, decimal Quantity, decimal UnitPrice)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (Status != InvoiceStatus.Draft)
        {
            return Error.Conflict("Invoice.NotDraft", "Only a draft invoice can be changed.");
        }

        if (lines.Count > MaxLines)
        {
            return Error.Validation("Invoice.TooManyLines", $"An invoice has at most {MaxLines} lines.");
        }

        var created = new List<InvoiceLine>(lines.Count);
        for (var index = 0; index < lines.Count; index++)
        {
            var (description, quantity, unitPrice) = lines[index];
            var line = InvoiceLine.Create(Id, index, description, quantity, unitPrice);
            if (line.IsFailure)
            {
                return line.Error!;
            }

            created.Add(line.Value);
        }

        _lines.Clear();
        _lines.AddRange(created);
        Total = _lines.Sum(line => line.Amount);
        return Result.Success();
    }

    public Result Issue(string number, string billToName, string billToEmail, DateTimeOffset nowUtc)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return Error.Conflict("Invoice.NotDraft", "Only a draft invoice can be issued.");
        }

        if (_lines.Count == 0 || Total <= 0)
        {
            return Error.Conflict("Invoice.Empty", "Add at least one line with a positive total before issuing.");
        }

        if (string.IsNullOrWhiteSpace(number) || number.Length > NumberMaxLength)
        {
            throw new ArgumentException("An invoice number is required.", nameof(number));
        }

        Number = number;
        BillToName = billToName;
        BillToEmail = billToEmail;
        Status = InvoiceStatus.Issued;
        IssuedAtUtc = nowUtc;
        return Result.Success();
    }

    public Result MarkPaid(DateTimeOffset nowUtc)
    {
        if (Status != InvoiceStatus.Issued)
        {
            return Error.Conflict("Invoice.NotIssued", "Only an issued invoice can be marked as paid.");
        }

        Status = InvoiceStatus.Paid;
        PaidAtUtc = nowUtc;
        return Result.Success();
    }

    public Result Void(DateTimeOffset nowUtc)
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Issued))
        {
            return Error.Conflict("Invoice.CannotVoid", "A paid or void invoice cannot be voided.");
        }

        Status = InvoiceStatus.Void;
        VoidedAtUtc = nowUtc;
        return Result.Success();
    }

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();
}
