using Onyx.Domain.Common;

namespace Onyx.Application.Commerce;

internal static class CommerceErrors
{
    public static readonly Error NotSignedIn = Error.Unauthorized("Customer.NotSignedIn", "Sign in to continue.");
    public static readonly Error ProductNotFound = Error.NotFound("Product.NotFound", "Product not found.");
    public static readonly Error SubscriptionNotFound = Error.NotFound("Subscription.NotFound", "Subscription not found.");
    public static readonly Error InvoiceNotFound = Error.NotFound("Invoice.NotFound", "Invoice not found.");
    public static readonly Error CustomerNotFound = Error.NotFound("Customer.NotFound", "Customer not found.");
    public static readonly Error VersionRequired = Error.Validation("Commerce.VersionRequired", "Reload the page and try again.");

    public static string Language(string? language) => language == "en" ? "en" : "ar";
}
