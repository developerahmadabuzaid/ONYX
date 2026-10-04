using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Onyx.Contracts.Common;

namespace Onyx.Web.ApiClients;

/// <summary>The only place Onyx.Web turns calls into HTTP requests. Controllers and views use the typed clients.</summary>
public abstract class OnyxApiClientBase(HttpClient httpClient)
{
    public const string UnavailableCode = "Web.ApiUnavailable";

    protected Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken, string? bearerToken = null) =>
        SendAsync<T>(HttpMethod.Get, path, null, bearerToken, cancellationToken);

    protected Task<ApiResult<T>> PostAsync<T>(string path, object? body, CancellationToken cancellationToken, string? bearerToken = null) =>
        SendAsync<T>(HttpMethod.Post, path, body is null ? null : JsonContent.Create(body), bearerToken, cancellationToken);

    protected Task<ApiResult<T>> PutAsync<T>(string path, object body, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Put, path, JsonContent.Create(body), null, cancellationToken);

    protected Task<ApiResult<T>> DeleteAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Delete, path, null, null, cancellationToken);

    /// <summary>Forwards a single file as multipart form data (field name <c>file</c>).</summary>
    protected Task<ApiResult<T>> PostFileAsync<T>(string path, Stream content, string fileName, string? contentType, CancellationToken cancellationToken)
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var parsed) ? parsed : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return SendAsync<T>(HttpMethod.Post, path, form, null, cancellationToken);
    }

    /// <summary>Streams a binary response (e.g. a media file). Null when the API answers anything but success.</summary>
    protected async Task<ApiFileStream?> GetStreamAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            return null;
        }

        return new ApiFileStream(response, await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, HttpContent? content, string? bearerToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative)) { Content = content };
        if (bearerToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsTransportFailure(exception, cancellationToken))
        {
            return ApiResult<T>.Failure(new ApiProblem { Status = StatusCodes.Status503ServiceUnavailable, Code = UnavailableCode }, HttpStatusCode.ServiceUnavailable);
        }

        using (response)
        {
            return await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException or Polly.ExecutionRejectedException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            if (typeof(T) == typeof(NoContent) || response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return ApiResult<T>.Success(typeof(T) == typeof(NoContent) ? (T)(object)NoContent.Instance : default!, response.StatusCode);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
            return ApiResult<T>.Success(value!, response.StatusCode);
        }

        ApiProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken).ConfigureAwait(false);
        }

        return ApiResult<T>.Failure(problem ?? new ApiProblem { Status = (int)response.StatusCode, Title = response.ReasonPhrase }, response.StatusCode);
    }
}
