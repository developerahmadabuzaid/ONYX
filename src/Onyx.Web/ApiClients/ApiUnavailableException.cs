namespace Onyx.Web.ApiClients;

/// <summary>Onyx.API could not serve a request that a page cannot do without; rendered as a 503 page, the session is kept.</summary>
public sealed class ApiUnavailableException : Exception
{
    public ApiUnavailableException()
        : base("Onyx.API is unavailable.")
    {
    }

    public ApiUnavailableException(string message)
        : base(message)
    {
    }

    public ApiUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
