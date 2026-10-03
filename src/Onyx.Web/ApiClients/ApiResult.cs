using System.Net;
using Onyx.Contracts.Common;

namespace Onyx.Web.ApiClients;

/// <summary>Outcome of an Onyx.API call: a value, or the API's problem details. Never throws for expected API errors.</summary>
public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiProblem? problem, HttpStatusCode status)
    {
        Value = value;
        Problem = problem;
        StatusCode = status;
    }

    public T? Value { get; }

    public ApiProblem? Problem { get; }

    public HttpStatusCode StatusCode { get; }

    public bool IsSuccess => Problem is null;

    /// <summary>The API could not be reached (network, timeout, open circuit): not a decision by the API.</summary>
    public bool IsUnavailable => Problem?.Code == OnyxApiClientBase.UnavailableCode;

    public static ApiResult<T> Success(T value, HttpStatusCode status) => new(value, null, status);

    public static ApiResult<T> Failure(ApiProblem problem, HttpStatusCode status) => new(default, problem, status);
}
