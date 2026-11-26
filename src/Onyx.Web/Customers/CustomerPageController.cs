using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Onyx.Contracts.Common;
using Onyx.Web.ApiClients;
using Onyx.Web.Configuration;
using Onyx.Web.Localization;
using Onyx.Web.Resources;

namespace Onyx.Web.Customers;

/// <summary>Shared plumbing of the customer pages: private (noindex) metadata, Turnstile key, return paths and API errors.</summary>
public abstract class CustomerPageController(
    LocalizedRouteTable routes, LanguageCatalog languages, SeoContext seo, IOptions<TurnstileOptions> turnstile, IStringLocalizer<SharedResource> localizer) : Controller
{
    public const string NoticeKey = "onyx.customer.notice";

    protected IStringLocalizer<SharedResource> Text => localizer;

    protected LocalizedRouteTable Routes => routes;

    protected void Describe(string pageKey, string lang, string titleKey, params string[] slugs)
    {
        seo.Title = localizer[titleKey].Value;
        seo.NoIndex = true;
        seo.NoFollow = true;
        foreach (var language in languages.Languages.Select(language => language.Code))
        {
            if (routes.PathFor(pageKey, language, slugs) is { } path)
            {
                seo.Alternates[language] = path;
            }
        }

        seo.CanonicalPath = routes.PathFor(pageKey, lang, slugs);
        ViewData["TurnstileSiteKey"] = turnstile.Value.IsConfigured ? turnstile.Value.SiteKey : null;
        ViewData["Nav"] = "account";
    }

    protected string PathOf(string pageKey, string lang, params string[] slugs) => routes.PathFor(pageKey, lang, slugs) ?? "/" + lang + "/";

    protected string? TurnstileToken() => Request.HasFormContentType ? Request.Form["cf-turnstile-response"].ToString() : null;

    /// <summary>Shows the API's reason in the visitor's language: a known code has its own text, anything else a general message.</summary>
    protected void AddApiError<T>(ApiResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        ModelState.AddModelError(string.Empty, ErrorText(result.Problem));
    }

    protected string ErrorText(ApiProblem? problem)
    {
        var key = "Customer.Error." + (problem?.Code ?? "General");
        var text = localizer[key];
        return text.ResourceNotFound ? localizer["Customer.Error.General"].Value : text.Value;
    }

    protected void Require(bool condition, string field, string key)
    {
        if (!condition)
        {
            ModelState.AddModelError(field, localizer[key].Value);
        }
    }
}
