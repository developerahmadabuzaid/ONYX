using Onyx.Contracts.Products;

namespace Onyx.Web.Customers;

public sealed record SubscribePage(PublicProductResponse Product, string Intent, SubscribeForm Form);
