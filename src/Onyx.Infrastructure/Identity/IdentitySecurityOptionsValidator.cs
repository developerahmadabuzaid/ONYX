using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Identity;

public sealed class IdentitySecurityOptionsValidator : IValidateOptions<IdentitySecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentitySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var valid = Uri.TryCreate(options.AdminPortalBaseUrl, UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback))
            && string.IsNullOrEmpty(url.Query)
            && string.IsNullOrEmpty(url.Fragment);

        return valid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("IdentitySecurity:AdminPortalBaseUrl must be an absolute https URL (http only for localhost) without query or fragment.");
    }
}
