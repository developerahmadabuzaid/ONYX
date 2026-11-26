using Onyx.Contracts.Products;

namespace Onyx.Web.Areas.Admin.Commerce;

public sealed record ProductEditPage(AdminProductResponse Product, ProductForm Form, bool CanEdit, bool CanPublish);
