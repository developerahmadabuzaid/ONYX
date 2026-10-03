namespace Onyx.Web.Authentication;

/// <summary>The API no longer accepts this admin session (revoked, expired, user disabled). The Admin area signs the user out.</summary>
public sealed class AdminSessionEndedException : Exception
{
    public AdminSessionEndedException()
        : base("The admin session has ended.")
    {
    }

    public AdminSessionEndedException(string message)
        : base(message)
    {
    }

    public AdminSessionEndedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
