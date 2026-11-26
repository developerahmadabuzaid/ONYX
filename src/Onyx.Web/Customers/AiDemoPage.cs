using Onyx.Contracts.Customers;
using Onyx.Contracts.Products;

namespace Onyx.Web.Customers;

public sealed record AiDemoPage(PublicProductResponse Product, IReadOnlyList<AiDemoRunResponse> Runs, AiDemoForm Form, bool Available);
