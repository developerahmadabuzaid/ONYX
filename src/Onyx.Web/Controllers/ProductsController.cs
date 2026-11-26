using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Web.ApiClients;
using Onyx.Web.Customers;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

/// <summary>
/// The public SaaS products (<c>/ar/المنتجات</c>, <c>/en/products</c>): browsing, plans, prices (only where made public) and demos
/// without an account. Trial, subscription and AI demo buttons lead to customer pages that keep the choice through sign-in.
/// </summary>
public sealed class ProductsController(
    IProductsApiClient api, LocalizedRouteTable routes, LanguageCatalog languages, SeoContext seo, IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string lang, string? slug, CancellationToken cancellationToken)
    {
        ViewData["Nav"] = "products";
        if (string.IsNullOrEmpty(slug))
        {
            var list = await api.ListAsync(lang, cancellationToken);
            if (list.IsUnavailable)
            {
                throw new ApiUnavailableException();
            }

            seo.Title = localizer["Products.Title"].Value;
            seo.Description = localizer["Products.Intro"].Value;
            SetAlternates(lang, language => routes.PathFor(CustomerPages.Products, language));
            return View(list.Value ?? []);
        }

        var product = await api.GetAsync(lang, slug, cancellationToken);
        if (!product.IsSuccess)
        {
            return product.IsUnavailable ? throw new ApiUnavailableException() : NotFound();
        }

        var value = product.Value!;
        seo.Title = value.Name;
        seo.Description = value.Summary;
        SetAlternates(lang, language => value.Slugs.TryGetValue(language, out var localized) ? routes.PathFor(CustomerPages.Products, language, localized) : null);
        return View("Show", value);
    }

    private void SetAlternates(string lang, Func<string, string?> pathFor)
    {
        foreach (var language in languages.Languages.Select(language => language.Code))
        {
            if (pathFor(language) is { } path)
            {
                seo.Alternates[language] = path;
            }
        }

        seo.CanonicalPath = pathFor(lang);
    }
}
