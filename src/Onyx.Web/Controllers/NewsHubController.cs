using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Onyx.Contracts.News;
using Onyx.Web.ApiClients;
using Onyx.Web.Localization;
using Onyx.Web.News;
using Onyx.Web.Resources;

namespace Onyx.Web.Controllers;

/// <summary>
/// The public News Hub (<c>/ar/آخر-الأخبار/…</c>, <c>/en/news/…</c>): the hub, category pages and items, from the API, which
/// returns published items only. The hub and categories are indexable when they list items (first page only); items are
/// <c>noindex, follow</c> unless an editor marked them indexable, in which case they also declare their language alternates.
/// </summary>
public sealed class NewsHubController(IPublicNewsApiClient api, SeoContext seo, IStringLocalizer<SharedResource> localizer) : Controller
{
    private const int DescriptionLength = 160;

    [HttpGet]
    public async Task<IActionResult> Index(string lang, string? slug, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        var current = Math.Clamp(page, 1, NewsPages.MaxPage);
        if (string.IsNullOrEmpty(slug))
        {
            var hub = await api.GetHubAsync(lang, current, cancellationToken);
            return hub.IsUnavailable ? throw new ApiUnavailableException() : hub.IsSuccess && hub.Value is { } list ? ShowList(list) : NotFound();
        }

        if (slug.Contains('/', StringComparison.Ordinal))
        {
            return NotFound();
        }

        var resolved = await api.ResolveAsync(lang, slug, current, cancellationToken);
        if (resolved.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        return resolved.Value switch
        {
            { List: { } category } when resolved.IsSuccess => ShowList(category),
            { Item: { } item } when resolved.IsSuccess => ShowItem(item),
            _ => NotFound(),
        };
    }

    private ViewResult ShowList(PublicNewsListResponse list)
    {
        seo.Title = list.Category?.Name ?? localizer["News.Title"].Value;
        var path = list.Category?.Path ?? list.Alternates.GetValueOrDefault(list.LanguageCode);
        seo.CanonicalPath = path is null ? null : SitePaths.Encode(path) + (list.Page > 1 ? $"?page={list.Page}" : string.Empty);

        // Only the first page of a hub or category that actually lists items is offered to search engines.
        seo.NoIndex = list.TotalCount == 0 || list.Page > 1;
        seo.NoFollow = false;
        if (!seo.NoIndex)
        {
            foreach (var (language, alternate) in list.Alternates)
            {
                seo.Alternates[language] = SitePaths.Encode(alternate);
            }
        }

        return View("List", list);
    }

    private ViewResult ShowItem(PublicNewsItemResponse item)
    {
        seo.Title = item.Title;
        seo.Description = item.Summary.Length > DescriptionLength ? item.Summary[..DescriptionLength].TrimEnd() + "…" : item.Summary;
        seo.CanonicalPath = SitePaths.Encode(item.Path);
        seo.NoIndex = !item.Indexable;
        seo.NoFollow = false;
        foreach (var (language, alternate) in item.Alternates)
        {
            seo.Alternates[language] = SitePaths.Encode(alternate);
        }

        return View("Item", item);
    }
}
