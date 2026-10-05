using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Onyx.Domain.Common;

namespace Onyx.API.Http;

/// <summary>Maps application results to RFC 9457 responses with a stable <c>code</c> extension.</summary>
internal static class ResultHttpExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static ActionResult OkOrProblem<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess ? controller.Ok(result.Value) : controller.Problem(result.Error!);

    public static ActionResult NoContentOrProblem(this ControllerBase controller, Result result) =>
        result.IsSuccess ? controller.NoContent() : controller.Problem(result.Error!);

    public static ActionResult Problem(this ControllerBase controller, Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        ProblemDetails problem;
        if (error.Type == ErrorType.Validation)
        {
            var modelState = new ModelStateDictionary();
            foreach (var (field, messages) in error.ValidationErrors ?? new Dictionary<string, string[]>())
            {
                foreach (var message in messages)
                {
                    modelState.AddModelError(field, message);
                }
            }

            problem = controller.ProblemDetailsFactory.CreateValidationProblemDetails(controller.HttpContext, modelState, status, error.Message);
        }
        else
        {
            problem = controller.ProblemDetailsFactory.CreateProblemDetails(controller.HttpContext, status, error.Message);
        }

        problem.Extensions["code"] = error.Code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { ProblemJson } };
    }
}
