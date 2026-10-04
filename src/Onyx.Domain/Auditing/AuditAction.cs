namespace Onyx.Domain.Auditing;

/// <summary>Stored as text. Append new values only; never renumber or rename existing ones.</summary>
public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    SoftDeleted = 3,

    // Security events (Stage 3).
    LoginSucceeded = 4,
    LoginFailed = 5,
    LockedOut = 6,
    TwoFactorEnabled = 7,
    TwoFactorReset = 8,
    TwoFactorRecoveryCodeUsed = 9,
    PasswordChanged = 10,
    PasswordReset = 11,
    RefreshTokenReuseDetected = 12,
    SessionRevoked = 13,
    Logout = 14,
    UserInvited = 15,
    InvitationAccepted = 16,
    UserUpdated = 17,
    UserDisabled = 18,
    UserEnabled = 19,
    UserRolesChanged = 20,
    RoleCreated = 21,
    RoleUpdated = 22,
    RoleDeleted = 23,
    InitialAdministratorCreated = 24,
    ProviderConnectionDisabled = 25,
    ProviderConnectionEnabled = 26,
    ProviderTestRequested = 27,
    TwoFactorRecoveredByOperator = 28,
}
