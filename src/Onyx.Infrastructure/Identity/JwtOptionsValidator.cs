using Microsoft.Extensions.Options;

namespace Onyx.Infrastructure.Identity;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            if (options.GetSigningKeyBytes().Length < JwtOptions.MinimumKeyBytes)
            {
                return ValidateOptionsResult.Fail($"Jwt:SigningKey must decode to at least {JwtOptions.MinimumKeyBytes} bytes.");
            }
        }
        catch (FormatException)
        {
            return ValidateOptionsResult.Fail("Jwt:SigningKey must be configured as base64 (user secrets locally, environment variables in production).");
        }

        return ValidateOptionsResult.Success;
    }
}
