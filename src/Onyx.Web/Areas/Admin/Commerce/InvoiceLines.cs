using System.Globalization;
using Onyx.Contracts.Commerce;

namespace Onyx.Web.Areas.Admin.Commerce;

/// <summary>Parses and formats invoice lines as "description | quantity | unit price" rows (invariant numbers).</summary>
public static class InvoiceLines
{
    public static IReadOnlyList<InvoiceLineRequest>? Parse(string? text)
    {
        var lines = new List<InvoiceLineRequest>();
        foreach (var row in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = row.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3
                || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity)
                || !decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var unitPrice))
            {
                return null;
            }

            lines.Add(new InvoiceLineRequest(parts[0], quantity, unitPrice));
        }

        return lines;
    }

    public static string Format(IEnumerable<InvoiceLineResponse> lines) =>
        string.Join('\n', lines.Select(line => string.Create(CultureInfo.InvariantCulture, $"{line.Description} | {line.Quantity} | {line.UnitPrice}")));
}
