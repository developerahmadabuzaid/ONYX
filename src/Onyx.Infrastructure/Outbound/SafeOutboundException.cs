namespace Onyx.Infrastructure.Outbound;

/// <summary>An outbound request was refused or failed. <see cref="Code"/> is one of the safe feed error codes; no URL is included.</summary>
public sealed class SafeOutboundException : Exception
{
    public SafeOutboundException()
        : this(Application.News.NewsFeedErrors.Unreachable, "The request failed.")
    {
    }

    public SafeOutboundException(string message)
        : this(Application.News.NewsFeedErrors.Unreachable, message)
    {
    }

    public SafeOutboundException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = Application.News.NewsFeedErrors.Unreachable;
    }

    public SafeOutboundException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public SafeOutboundException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }

    /// <summary>The server's status for <see cref="Application.News.NewsFeedErrors.HttpStatus"/> refusals.</summary>
    public int? StatusCode { get; init; }
}
