namespace Onyx.Infrastructure.Email;

/// <summary>Bound from the <c>Email</c> section. Validated by <see cref="EmailOptionsValidator"/> at Worker startup.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailTransport Transport { get; set; } = EmailTransport.None;

    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Onyx Vanguard Solutions";

    public SmtpSettings Smtp { get; set; } = new();

    public string? PickupDirectory { get; set; }
}
