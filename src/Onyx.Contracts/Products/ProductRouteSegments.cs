namespace Onyx.Contracts.Products;

/// <summary>The public products section: <c>/ar/المنتجات/{slug}</c>, <c>/en/products/{slug}</c>.</summary>
public static class ProductRouteSegments
{
    public static IReadOnlyDictionary<string, string> Products { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ar"] = "المنتجات",
        ["en"] = "products",
    };
}
