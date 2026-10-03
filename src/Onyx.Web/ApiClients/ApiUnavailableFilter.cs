using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Onyx.Web.ApiClients;

/// <summary>Turns <see cref="ApiUnavailableException"/> into a 503, rendered by the status-code error page.</summary>
public sealed partial class ApiUnavailableFilter(ILogger<ApiUnavailableFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Exception is not ApiUnavailableException)
        {
            return;
        }

        LogUnavailable(logger, context.HttpContext.Request.Path);
        context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        context.ExceptionHandled = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Onyx.API unavailable while serving {Path}")]
    private static partial void LogUnavailable(ILogger logger, string path);
}
