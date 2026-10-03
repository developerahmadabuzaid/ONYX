using System.Globalization;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Onyx.Application.Abstractions;
using Onyx.Application.Auditing;
using Onyx.Application.Common;
using Onyx.Application.Email;
using Onyx.Application.Identity;
using Onyx.Application.Notifications;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Identity;
using Onyx.Domain.Auditing;
using Onyx.Domain.Common;
using Onyx.Domain.Notifications;
using Onyx.Infrastructure.Email.Templates;
using Onyx.Infrastructure.Persistence;

namespace Onyx.Infrastructure.Identity;

/// <summary>
/// Admin authentication built on ASP.NET Core Identity primitives (no cookies: the API is bearer-only; Onyx.Web is the BFF).
/// Every credential failure returns the same generic error and equalized timing, so accounts cannot be enumerated.
/// </summary>
public sealed class AuthService(
    UserManager<ApplicationUser> users,
    ApplicationDbContext dbContext,
    PasswordTimingEqualizer timingEqualizer,
    IPermissionService permissions,
    SessionManager sessions,
    IAuditTrail auditTrail,
    IEmailOutbox emailOutbox,
    INotificationService notifications,
    AdminLinkBuilder links,
    ICurrentUser currentUser,
    IOptions<SessionOptions> sessionOptions,
    IOptions<IdentitySecurityOptions> securityOptions,
    TimeProvider timeProvider,
    IValidator<LoginRequest> loginValidator,
    IValidator<TwoFactorLoginRequest> twoFactorValidator,
    IValidator<TwoFactorEnrollmentStartRequest> enrollmentStartValidator,
    IValidator<TwoFactorEnrollmentCompleteRequest> enrollmentCompleteValidator,
    IValidator<RefreshTokenRequest> refreshValidator,
    IValidator<ForgotPasswordRequest> forgotValidator,
    IValidator<ResetPasswordRequest> resetValidator,
    IValidator<AcceptInvitationRequest> invitationValidator,
    IValidator<ChangePasswordRequest> changePasswordValidator) : IAuthService
{
    public const int RecoveryCodeCount = 10;
    private const string AuthenticatorIssuer = "Onyx Vanguard Solutions";

    private static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid credentials.");
    private static readonly Error InvalidChallenge = Error.Unauthorized("Auth.InvalidChallenge", "The sign-in step has expired. Sign in again.");
    private static readonly Error InvalidCode = Error.Unauthorized("Auth.InvalidCode", "Invalid verification code.");
    private static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "The session has ended. Sign in again.");
    private static readonly Error InvalidToken = Error.Validation("Auth.InvalidToken", "The link is invalid or has expired.");

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (await loginValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var user = await users.FindByEmailAsync(request.Email.Trim()).ConfigureAwait(false);
        if (user is null)
        {
            timingEqualizer.Burn(request.Password);
            await RecordAndSaveAsync(AuditAction.LoginFailed, null, new { reason = "UnknownAccount" }, cancellationToken).ConfigureAwait(false);
            return InvalidCredentials;
        }

        var ineligibleReason = !user.IsActive ? "Disabled"
            : !user.EmailConfirmed ? "NotActivated"
            : await users.IsLockedOutAsync(user).ConfigureAwait(false) ? "LockedOut"
            : null;
        if (ineligibleReason is not null)
        {
            timingEqualizer.Burn(request.Password);
            await RecordAndSaveAsync(AuditAction.LoginFailed, user.Id, new { reason = ineligibleReason }, cancellationToken).ConfigureAwait(false);
            return InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            await users.AccessFailedAsync(user).ConfigureAwait(false);
            var lockedOut = await users.IsLockedOutAsync(user).ConfigureAwait(false);
            await RecordAndSaveAsync(lockedOut ? AuditAction.LockedOut : AuditAction.LoginFailed, user.Id, new { reason = "InvalidPassword" }, cancellationToken).ConfigureAwait(false);
            return InvalidCredentials;
        }

        await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        if (user.TwoFactorEnabled)
        {
            var (challenge, expires) = await CreateChallengeAsync(user.Id, LoginChallengePurpose.TwoFactor, sessionOptions.Value.TwoFactorChallengeMinutes, cancellationToken).ConfigureAwait(false);
            return new LoginResponse(LoginStatus.TwoFactorRequired, null, challenge, expires);
        }

        var actor = await permissions.GetAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (TwoFactorPolicy.IsRequired(actor.IsSuperAdmin, actor.Permissions, securityOptions.Value.RequireTwoFactorForAllUsers))
        {
            var (challenge, expires) = await CreateChallengeAsync(user.Id, LoginChallengePurpose.TwoFactorEnrollment, sessionOptions.Value.EnrollmentChallengeMinutes, cancellationToken).ConfigureAwait(false);
            return new LoginResponse(LoginStatus.TwoFactorEnrollmentRequired, null, challenge, expires);
        }

        var tokens = await CompleteSignInAsync(user, cancellationToken).ConfigureAwait(false);
        return new LoginResponse(LoginStatus.Succeeded, tokens, null, null);
    }

    public async Task<Result<TokenPairResponse>> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        if (await twoFactorValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var challenge = await FindChallengeAsync(request.ChallengeToken, LoginChallengePurpose.TwoFactor, cancellationToken).ConfigureAwait(false);
        var user = challenge is null ? null : await users.FindByIdAsync(challenge.UserId.ToString()).ConfigureAwait(false);
        if (challenge is null || user is not { IsActive: true, TwoFactorEnabled: true } || await users.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            return InvalidChallenge;
        }

        var usedRecoveryCode = !string.IsNullOrWhiteSpace(request.RecoveryCode);
        var verified = usedRecoveryCode
            ? (await users.RedeemTwoFactorRecoveryCodeAsync(user, request.RecoveryCode!.Replace(" ", string.Empty, StringComparison.Ordinal)).ConfigureAwait(false)).Succeeded
            : await VerifyAuthenticatorCodeAsync(user, request.Code!).ConfigureAwait(false);

        if (!verified)
        {
            await RegisterFailedCodeAsync(challenge, user, cancellationToken).ConfigureAwait(false);
            return InvalidCode;
        }

        challenge.ConsumedAtUtc = timeProvider.GetUtcNow();
        if (usedRecoveryCode)
        {
            auditTrail.Record(AuditAction.TwoFactorRecoveryCodeUsed, AuditCategory.Security, "User", user.Id.ToString(), actorUserId: user.Id);
        }

        return await CompleteSignInAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<TwoFactorEnrollmentStartResponse>> StartTwoFactorEnrollmentAsync(TwoFactorEnrollmentStartRequest request, CancellationToken cancellationToken)
    {
        if (await enrollmentStartValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var challenge = await FindChallengeAsync(request.EnrollmentToken, LoginChallengePurpose.TwoFactorEnrollment, cancellationToken).ConfigureAwait(false);
        var user = challenge is null ? null : await users.FindByIdAsync(challenge.UserId.ToString()).ConfigureAwait(false);
        if (challenge is null || user is not { IsActive: true, TwoFactorEnabled: false })
        {
            return InvalidChallenge;
        }

        // A fresh key on every start: an abandoned enrolment never leaves a usable key behind.
        await users.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        var key = await users.GetAuthenticatorKeyAsync(user).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Authenticator key was not created.");

        var uri = string.Create(CultureInfo.InvariantCulture,
            $"otpauth://totp/{Uri.EscapeDataString(AuthenticatorIssuer)}:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer={Uri.EscapeDataString(AuthenticatorIssuer)}&digits=6");
        return new TwoFactorEnrollmentStartResponse(FormatKey(key), uri);
    }

    public async Task<Result<TwoFactorEnrollmentCompleteResponse>> CompleteTwoFactorEnrollmentAsync(TwoFactorEnrollmentCompleteRequest request, CancellationToken cancellationToken)
    {
        if (await enrollmentCompleteValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return invalid;
        }

        var challenge = await FindChallengeAsync(request.EnrollmentToken, LoginChallengePurpose.TwoFactorEnrollment, cancellationToken).ConfigureAwait(false);
        var user = challenge is null ? null : await users.FindByIdAsync(challenge.UserId.ToString()).ConfigureAwait(false);
        if (challenge is null || user is not { IsActive: true, TwoFactorEnabled: false })
        {
            return InvalidChallenge;
        }

        if (!await VerifyAuthenticatorCodeAsync(user, request.Code).ConfigureAwait(false))
        {
            await RegisterFailedCodeAsync(challenge, user, cancellationToken).ConfigureAwait(false);
            return InvalidCode;
        }

        await users.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        var recoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount).ConfigureAwait(false))?.ToList() ?? [];
        challenge.ConsumedAtUtc = timeProvider.GetUtcNow();
        auditTrail.Record(AuditAction.TwoFactorEnabled, AuditCategory.Security, "User", user.Id.ToString(), actorUserId: user.Id);

        var tokens = await CompleteSignInAsync(user, cancellationToken).ConfigureAwait(false);
        return new TwoFactorEnrollmentCompleteResponse(recoveryCodes, tokens);
    }

    public async Task<Result<TokenPairResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        if (await refreshValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is not null)
        {
            return InvalidRefreshToken;
        }

        var outcome = await sessions.RefreshAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        switch (outcome.Kind)
        {
            case RefreshOutcomeKind.Succeeded:
                return outcome.Tokens!;

            case RefreshOutcomeKind.ReuseDetected:
                await RecordAndSaveAsync(AuditAction.RefreshTokenReuseDetected, outcome.UserId, new { sessionId = outcome.SessionId }, cancellationToken).ConfigureAwait(false);
                await notifications.PublishAsync(
                    new NotificationRequest(
                        NotificationType.SecurityAlert,
                        NotificationSeverity.Critical,
                        Permissions.Users.Manage,
                        new Dictionary<string, string> { ["alert"] = "RefreshTokenReuse" },
                        ResourceType: "User",
                        ResourceId: outcome.UserId?.ToString()),
                    cancellationToken).ConfigureAwait(false);
                return InvalidRefreshToken;

            default:
                return InvalidRefreshToken;
        }
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var sessionId = await sessions.RevokeByRefreshTokenAsync(request.RefreshToken, SessionManager.ReasonLogout, cancellationToken).ConfigureAwait(false);
        if (sessionId is not null)
        {
            var userId = await dbContext.UserSessions.AsNoTracking().Where(session => session.Id == sessionId)
                .Select(session => session.UserId).SingleAsync(cancellationToken).ConfigureAwait(false);
            await RecordAndSaveAsync(AuditAction.Logout, userId, new { sessionId }, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<Result> RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        if (await forgotValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var user = await users.FindByEmailAsync(request.Email.Trim()).ConfigureAwait(false);
        if (user is not { IsActive: true, EmailConfirmed: true })
        {
            return Result.Success();
        }

        var token = await users.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var queued = await emailOutbox.QueueAsync(
            new EmailRequest(
                PasswordResetEmailTemplate.TemplateKey,
                user.PreferredLanguage,
                user.Email!,
                user.DisplayName,
                new Dictionary<string, string>
                {
                    ["name"] = user.DisplayName,
                    ["link"] = links.ResetPassword(user.Id, token).AbsoluteUri,
                    ["minutes"] = securityOptions.Value.PasswordResetLifetimeMinutes.ToString(CultureInfo.InvariantCulture),
                }),
            cancellationToken).ConfigureAwait(false);

        if (queued.IsSuccess)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        if (await resetValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var user = await users.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        var token = AdminLinkBuilder.DecodeToken(request.Token);
        if (user is not { IsActive: true, EmailConfirmed: true } || token is null)
        {
            return Result.Failure(InvalidToken);
        }

        var result = await users.ResetPasswordAsync(user, token, request.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Result.Failure(result.HasInvalidToken() ? InvalidToken : result.ToValidationError("newPassword"));
        }

        await users.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        await users.ResetAccessFailedCountAsync(user).ConfigureAwait(false);
        await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonPasswordReset, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.PasswordReset, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        if (await invitationValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var user = await users.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        var token = AdminLinkBuilder.DecodeToken(request.Token);
        if (user is not { IsActive: true, EmailConfirmed: false } || token is null
            || !await users.VerifyUserTokenAsync(user, InvitationTokenProviderOptions.ProviderName, InvitationTokenProviderOptions.Purpose, token).ConfigureAwait(false))
        {
            return Result.Failure(InvalidToken);
        }

        var added = await users.AddPasswordAsync(user, request.NewPassword).ConfigureAwait(false);
        if (!added.Succeeded)
        {
            return Result.Failure(added.ToValidationError("newPassword"));
        }

        // Possession of the emailed token proves ownership of the address; the stamp change makes the link single-use.
        user.EmailConfirmed = true;
        await users.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.InvitationAccepted, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (await changePasswordValidator.ValidateToErrorAsync(request, cancellationToken).ConfigureAwait(false) is { } invalid)
        {
            return Result.Failure(invalid);
        }

        var user = await GetCurrentUserEntityAsync().ConfigureAwait(false);
        if (user is null)
        {
            return Result.Failure(InvalidCredentials);
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Result.Failure(result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? Error.Validation("Auth.InvalidPassword", "The current password is incorrect.", new Dictionary<string, string[]> { ["currentPassword"] = ["The current password is incorrect."] })
                : result.ToValidationError("newPassword"));
        }

        await sessions.RevokeAllAsync(user.Id, SessionManager.ReasonPasswordChanged, cancellationToken).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.PasswordChanged, user.Id, null, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserEntityAsync().ConfigureAwait(false);
        if (user is null)
        {
            return InvalidCredentials;
        }

        var actor = await permissions.GetAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return new CurrentUserResponse(user.Id, user.Email!, user.DisplayName, user.PreferredLanguage, user.TwoFactorEnabled, actor.Permissions.Order(StringComparer.Ordinal).ToList());
    }

    private async Task<ApplicationUser?> GetCurrentUserEntityAsync() =>
        currentUser.UserId is { } userId ? await users.FindByIdAsync(userId.ToString()).ConfigureAwait(false) : null;

    private async Task<TokenPairResponse> CompleteSignInAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.LastLoginAtUtc = timeProvider.GetUtcNow();
        var tokens = sessions.Start(user);
        auditTrail.Record(AuditAction.LoginSucceeded, AuditCategory.Security, "User", user.Id.ToString(), actorUserId: user.Id);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return tokens;
    }

    private async Task<bool> VerifyAuthenticatorCodeAsync(ApplicationUser user, string code)
    {
        var key = await users.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key) || !Totp.TryMatch(Totp.DecodeBase32(key), code, timeProvider.GetUtcNow(), user.LastTotpTimeStep, out var step))
        {
            return false;
        }

        user.LastTotpTimeStep = step;
        return true;
    }

    private async Task RegisterFailedCodeAsync(LoginChallenge challenge, ApplicationUser user, CancellationToken cancellationToken)
    {
        challenge.FailedAttempts++;
        if (challenge.FailedAttempts >= sessionOptions.Value.MaxChallengeAttempts)
        {
            challenge.ConsumedAtUtc = timeProvider.GetUtcNow();
        }

        await users.AccessFailedAsync(user).ConfigureAwait(false);
        await RecordAndSaveAsync(AuditAction.LoginFailed, user.Id, new { reason = "InvalidSecondFactor" }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string Token, DateTimeOffset ExpiresAtUtc)> CreateChallengeAsync(
        Guid userId, LoginChallengePurpose purpose, int lifetimeMinutes, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var raw = TokenHashing.NewToken();
        var expires = now.AddMinutes(lifetimeMinutes);
        dbContext.LoginChallenges.Add(new LoginChallenge
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = TokenHashing.Hash(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (raw, expires);
    }

    private Task<LoginChallenge?> FindChallengeAsync(string rawToken, LoginChallengePurpose purpose, CancellationToken cancellationToken)
    {
        var hash = TokenHashing.Hash(rawToken);
        var now = timeProvider.GetUtcNow();
        var maxAttempts = sessionOptions.Value.MaxChallengeAttempts;
        return dbContext.LoginChallenges.SingleOrDefaultAsync(
            challenge => challenge.TokenHash == hash && challenge.Purpose == purpose && challenge.ConsumedAtUtc == null
                && challenge.ExpiresAtUtc > now && challenge.FailedAttempts < maxAttempts,
            cancellationToken);
    }

    private async Task RecordAndSaveAsync(AuditAction action, Guid? userId, object? details, CancellationToken cancellationToken)
    {
        auditTrail.Record(action, AuditCategory.Security, "User", userId?.ToString(), details, userId);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatKey(string key) =>
        string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4).Select(index => key.Substring(index * 4, Math.Min(4, key.Length - (index * 4))))).ToLowerInvariant();
}
