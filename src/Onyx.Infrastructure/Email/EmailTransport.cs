namespace Onyx.Infrastructure.Email;

public enum EmailTransport
{
    /// <summary>No transport configured: emails stay queued and the provider shows "Not connected".</summary>
    None = 0,

    /// <summary>SMTP (also covers Microsoft 365 SMTP AUTH).</summary>
    Smtp = 1,

    /// <summary>Development only: writes .eml files to a local folder instead of sending.</summary>
    PickupDirectory = 2,
}
