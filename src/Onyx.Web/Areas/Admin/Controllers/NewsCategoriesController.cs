using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.News;
using Onyx.Web.ApiClients;
using Onyx.Web.Areas.Admin.Cms;

namespace Onyx.Web.Areas.Admin.Controllers;

/// <summary>News categories: names and slugs in both languages, and the keywords used by the rule-based classifier.</summary>
[Route("admin/news/categories")]
public sealed class NewsCategoriesController(IAdminNewsApiClient api) : CmsControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await api.ListCategoriesAsync(cancellationToken);
        return Failed(result) ?? View(result.Value);
    }

    [HttpPost("")]
    public async Task<IActionResult> Create(CategoryForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.CreateCategoryAsync(form.ToRequest(null), cancellationToken), RedirectToAction(nameof(Index)), "Cms.Created");
    }

    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CategoryForm form, uint version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        return After(await api.UpdateCategoryAsync(id, form.ToRequest(version), cancellationToken), RedirectToAction(nameof(Index)));
    }

    public sealed class CategoryForm
    {
        public string? Key { get; set; }

        public int SortOrder { get; set; }

        public string? Keywords { get; set; }

        public string? NameAr { get; set; }

        public string? SlugAr { get; set; }

        public string? NameEn { get; set; }

        public string? SlugEn { get; set; }

        public SaveNewsCategoryRequest ToRequest(uint? version) =>
            new(
                Key ?? string.Empty,
                SortOrder,
                SplitList(Keywords),
                [new NewsCategoryTranslationDto("ar", NameAr ?? string.Empty, Blank(SlugAr)), new NewsCategoryTranslationDto("en", NameEn ?? string.Empty, Blank(SlugEn))],
                version);
    }
}
