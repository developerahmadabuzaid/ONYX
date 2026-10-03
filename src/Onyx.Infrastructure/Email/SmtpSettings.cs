namespace Onyx.Infrastructure.Email;

public sealed class SmtpSettings
{
    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? Username { get; set; }

    /// <summary>Secret: user secrets locally, environment variable / secret store in production. Never logged.</summary>
    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}
