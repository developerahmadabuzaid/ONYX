using FluentValidation;
using Onyx.Contracts.Administration;
using Onyx.Contracts.Authorization;
using Onyx.Contracts.Identity;

namespace Onyx.Application.Identity.Validation;

internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(IdentityRules.PasswordMaxLength);
        RuleFor(request => request.CaptchaToken).MaximumLength(2_048);
    }
}

internal sealed class TwoFactorLoginRequestValidator : AbstractValidator<TwoFactorLoginRequest>
{
    public TwoFactorLoginRequestValidator()
    {
        RuleFor(request => request.ChallengeToken).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
        RuleFor(request => request)
            .Must(request => string.IsNullOrWhiteSpace(request.Code) != string.IsNullOrWhiteSpace(request.RecoveryCode))
            .WithMessage("Provide either an authenticator code or a recovery code.");
        RuleFor(request => request.Code).Matches("^[0-9]{6}$").When(request => !string.IsNullOrWhiteSpace(request.Code));
        RuleFor(request => request.RecoveryCode).MaximumLength(64);
    }
}

internal sealed class TwoFactorEnrollmentStartRequestValidator : AbstractValidator<TwoFactorEnrollmentStartRequest>
{
    public TwoFactorEnrollmentStartRequestValidator() =>
        RuleFor(request => request.EnrollmentToken).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
}

internal sealed class TwoFactorEnrollmentCompleteRequestValidator : AbstractValidator<TwoFactorEnrollmentCompleteRequest>
{
    public TwoFactorEnrollmentCompleteRequestValidator()
    {
        RuleFor(request => request.EnrollmentToken).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
        RuleFor(request => request.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}

internal sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator() =>
        RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
}

internal sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
        RuleFor(request => request.CaptchaToken).MaximumLength(2_048);
    }
}

internal sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Token).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
        RuleFor(request => request.NewPassword).ValidNewPassword();
    }
}

internal sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.Token).NotEmpty().MaximumLength(IdentityRules.TokenMaxLength);
        RuleFor(request => request.NewPassword).ValidNewPassword();
    }
}

internal sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(IdentityRules.PasswordMaxLength);
        RuleFor(request => request.NewPassword).ValidNewPassword().NotEqual(request => request.CurrentPassword);
    }
}

internal sealed class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(request => request.Email).ValidEmail();
        RuleFor(request => request.DisplayName).ValidDisplayName();
        RuleFor(request => request.PreferredLanguage).ValidLanguage();
        RuleFor(request => request.Roles).NotNull().Must(roles => roles.Count <= 10);
    }
}

internal sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(request => request.DisplayName).ValidDisplayName();
        RuleFor(request => request.PreferredLanguage).ValidLanguage();
    }
}

internal sealed class SetUserRolesRequestValidator : AbstractValidator<SetUserRolesRequest>
{
    public SetUserRolesRequestValidator() => RuleFor(request => request.Roles).NotNull().Must(roles => roles.Count <= 10);
}

internal sealed class SaveRoleRequestValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleRequestValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(50).Matches("^[A-Za-z][A-Za-z0-9 _-]*$");
        RuleFor(request => request.Permissions).NotNull()
            .Must(permissions => permissions.Distinct(StringComparer.Ordinal).Count() == permissions.Count)
            .WithMessage("Permissions must be unique.");
        RuleForEach(request => request.Permissions)
            .Must(permission => Permissions.All.Contains(permission))
            .WithMessage("'{PropertyValue}' is not a known permission.");
    }
}
