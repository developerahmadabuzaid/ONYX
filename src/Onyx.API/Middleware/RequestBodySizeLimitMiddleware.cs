using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Onyx.API.Configuration;

namespace Onyx.API.Middleware;

/// <summary>
/// Enforces the global body cap on every server (Kestrel also enforces it). Declared oversized bodies are rejected before reading;
/// chunked bodies are capped through <see cref="IHttpMaxRequestBodySizeFeature"/> and fail with 413 when the limit is crossed.
/// </summary>
public sealed class RequestBodySizeLimitMiddleware(RequestDelegate next, IOptions<ApiSecurityOptions> options)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problemDetails);

        var limit = options.Value.MaxRequestBodySizeBytes;
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false } && (sizeFeature.MaxRequestBodySize is null || sizeFeature.MaxRequestBodySize > limit))
        {
            sizeFeature.MaxRequestBodySize = limit;
        }

        if (context.Request.ContentLength > limit)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Request body too large.",
                },
            }).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
