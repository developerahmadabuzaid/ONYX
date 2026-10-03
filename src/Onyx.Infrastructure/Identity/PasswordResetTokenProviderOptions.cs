using Microsoft.AspNetCore.Identity;

namespace Onyx.Infrastructure.Identity;

public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "OnyxPasswordReset";

    public PasswordResetTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(1);
    }
}
