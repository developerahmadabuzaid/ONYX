using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Onyx.Application.Abstractions.Persistence;

namespace Onyx.API.ErrorHandling;

/// <summary>
/// Last-resort handler for unexpected exceptions. Logs the full exception server-side and returns a generic RFC 9457 response:
/// no exception type, message or stack trace ever reaches the client, in any environment.
/// </summary>
public sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (status, title) = exception switch
        {
            BadHttpRequestException badRequest => (badRequest.StatusCode, "The request could not be processed."),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "The record was changed by someone else. Reload and try again."),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "Request cancelled."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = exception is ConcurrencyConflictException
                ? new ProblemDetails { Status = status, Title = title, Extensions = { ["code"] = "Concurrency.Conflict" } }
                : new ProblemDetails { Status = status, Title = title },
        }).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, PathString path);
}
