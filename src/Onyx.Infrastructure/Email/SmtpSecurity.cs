namespace Onyx.Infrastructure.Email;

public enum SmtpSecurity
{
    StartTls = 0,
    SslOnConnect = 1,

    /// <summary>Plaintext. Allowed only in Development (e.g. a local mail catcher).</summary>
    None = 2,
}
