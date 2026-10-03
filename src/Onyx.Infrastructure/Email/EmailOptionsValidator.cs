using MimeKit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Email;

/// <summary>Fails startup on unsafe or incomplete email configuration rather than discovering it on the first send.</summary>
public sealed class EmailOptionsValidator(IHostEnvironment environment) : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (options.Transport != EmailTransport.None
            && (string.IsNullOrWhiteSpace(options.FromAddress) || !MailboxAddress.TryParse(options.FromAddress, out _)))
        {
            failures.Add("Email:FromAddress must be a valid address when a transport is configured.");
        }

        if (options.FromName.IndexOfAny(['\r', '\n']) >= 0)
        {
            failures.Add("Email:FromName must not contain line breaks.");
        }

        if (options.Transport == EmailTransport.Smtp)
        {
            if (string.IsNullOrWhiteSpace(options.Smtp.Host))
            {
                failures.Add("Email:Smtp:Host is required for the SMTP transport.");
            }

            if (options.Smtp.Port is < 1 or > 65_535)
            {
                failures.Add("Email:Smtp:Port must be between 1 and 65535.");
            }

            if (options.Smtp.TimeoutSeconds is < 5 or > 300)
            {
                failures.Add("Email:Smtp:TimeoutSeconds must be between 5 and 300.");
            }

            if (options.Smtp.Security == SmtpSecurity.None && !environment.IsDevelopment())
            {
                failures.Add("Plaintext SMTP (Email:Smtp:Security=None) is only allowed in Development.");
            }

            if (string.IsNullOrEmpty(options.Smtp.Username) != string.IsNullOrEmpty(options.Smtp.Password))
            {
                failures.Add("Email:Smtp:Username and Email:Smtp:Password must be set together.");
            }
        }

        if (options.Transport == EmailTransport.PickupDirectory)
        {
            if (!environment.IsDevelopment())
            {
                failures.Add("The PickupDirectory email transport is only allowed in Development.");
            }

            if (string.IsNullOrWhiteSpace(options.PickupDirectory))
            {
                failures.Add("Email:PickupDirectory is required for the PickupDirectory transport.");
            }
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
