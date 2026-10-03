namespace Onyx.Application.Email;

/// <summary>
/// Transport-neutral send failure. <see cref="Exception.Message"/> is a safe, credential-free description that may be
/// stored and shown to administrators; the provider's original exception is kept only as <see cref="Exception.InnerException"/>.
/// </summary>
public sealed class EmailTransportException : Exception
{
    public EmailTransportException()
    {
    }

    public EmailTransportException(string message)
        : base(message)
    {
    }

    public EmailTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public EmailTransportException(EmailFailureKind kind, string safeMessage, Exception? innerException = null)
        : base(safeMessage, innerException)
    {
        Kind = kind;
    }

    public EmailFailureKind Kind { get; }
}
