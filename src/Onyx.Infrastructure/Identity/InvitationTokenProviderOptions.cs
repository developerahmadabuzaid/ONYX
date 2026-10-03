using Microsoft.AspNetCore.Identity;

namespace Onyx.Infrastructure.Identity;

public sealed class InvitationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public const string ProviderName = "OnyxInvitation";
    public const string Purpose = "AcceptInvitation";

    public InvitationTokenProviderOptions()
    {
        Name = ProviderName;
        TokenLifespan = TimeSpan.FromHours(72);
    }
}
