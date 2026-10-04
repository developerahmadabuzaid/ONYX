namespace Onyx.Application.Identity;

/// <summary>Bounds for the operator 2FA recovery command's audit reason.</summary>
public static class IdentityRecoveryLimits
{
    public const int ReasonMinLength = 10;
    public const int ReasonMaxLength = 500;
}
