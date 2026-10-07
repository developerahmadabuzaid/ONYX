using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.Search;
using Onyx.Web.ApiClients;
using Onyx.Web.Localization;
using Onyx.Web.Resources;
using Onyx.Web.Search;

namespace Onyx.Web.Controllers;

/// <summary>
/// The site search page (<c>/ar/بحث</c>, <c>/en/search</c>): GET only, never indexed and never cached, results grouped by type.
/// Results come from the API, which searches published public content only.
/// </summary>
public sealed class SiteSearchController(
    ISearchApiClient api, LocalizedRouteTable routes, LanguageCatalog languages, SeoContext seo, IStringLocalizer<SharedResource> localizer) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string lang, string? slug, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (slug is not null)
        {
            return NotFound();
        }

        seo.Title = localizer["Search.Title"].Value;
        seo.NoIndex = true;
        seo.NoFollow = false;
        foreach (var language in languages.Languages.Select(language => language.Code))
        {
            if (routes.PathFor(SearchPages.Search, language) is { } path)
            {
                seo.Alternates[language] = path;
            }
        }

        Response.Headers.CacheControl = "no-store";
        var query = (q ?? string.Empty).Trim();
        if (query.Length > SearchPages.MaxQueryLength)
        {
            query = query[..SearchPages.MaxQueryLength];
        }

        if (query.Length == 0)
        {
            return View(new SearchPageModel(query, null, false));
        }

        var result = await api.SearchAsync(lang, query, cancellationToken);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        return View(new SearchPageModel(query, result.IsSuccess ? result.Value : null, result.StatusCode == System.Net.HttpStatusCode.TooManyRequests));
    }
}
