using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Onyx.Contracts.Common;
using Onyx.Web.ApiClients;
using Onyx.Web.Security;

namespace Onyx.Web.Areas.Admin.Cms;

/// <summary>
/// Shared plumbing for the Admin CMS screens. The API authorizes every call; this only translates API outcomes into pages:
/// 403 → access denied, 404 → not found, unreachable API → 503, other problems → a one-time error message.
/// </summary>
[Area("Admin")]
[Authorize]
[SensitivePage]
[TypeFilter<AdminShellFilter>]
public abstract class CmsControllerBase : Controller
{
    public const string NoticeKey = "onyx.notice";
    public const string ErrorKey = "onyx.error";

    /// <summary>For page loads: null when the call succeeded, otherwise the result to return.</summary>
    protected IActionResult? Failed<T>(ApiResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            return null;
        }

        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        return result.StatusCode switch
        {
            HttpStatusCode.Forbidden => Forbid(),
            HttpStatusCode.NotFound => NotFound(),
            _ => throw new ApiUnavailableException($"Unexpected API status {(int)result.StatusCode}."),
        };
    }

    /// <summary>For commands: records a notice or the API's error, then continues with <paramref name="next"/> (usually a redirect).</summary>
    protected IActionResult After<T>(ApiResult<T> result, IActionResult next, string notice = "Cms.Saved")
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        if (result.StatusCode == HttpStatusCode.Forbidden)
        {
            return Forbid();
        }

        if (result.IsSuccess)
        {
            TempData[NoticeKey] = notice;
        }
        else
        {
            TempData[ErrorKey] = Describe(result.Problem);
        }

        return next;
    }

    /// <summary>Keeps the API's message for the form being re-displayed.</summary>
    protected void AddProblem<T>(ApiResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsUnavailable)
        {
            throw new ApiUnavailableException();
        }

        ModelState.AddModelError(string.Empty, Describe(result.Problem));
    }

    protected IActionResult ShowError(string message, IActionResult next)
    {
        TempData[ErrorKey] = message;
        return next;
    }

    /// <summary>The API's code and messages; they never contain secrets (ProblemDetails are already safe for clients).</summary>
    public static string Describe(ApiProblem? problem)
    {
        if (problem is null)
        {
            return "Error";
        }

        var parts = new List<string>();
        var headline = problem.Detail ?? problem.Title;
        parts.Add(string.IsNullOrEmpty(problem.Code) ? headline ?? "Error" : $"{problem.Code}: {headline}");
        if (problem.Errors is not null)
        {
            parts.AddRange(problem.Errors.SelectMany(pair => pair.Value.Select(message => string.IsNullOrEmpty(pair.Key) ? message : $"{pair.Key}: {message}")));
        }

        return string.Join(" | ", parts.Distinct(StringComparer.Ordinal));
    }

    protected static IReadOnlyList<string> SplitList(string? value) =>
        (value ?? string.Empty).Split([',', '\n', '\r', '،'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();

    protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
