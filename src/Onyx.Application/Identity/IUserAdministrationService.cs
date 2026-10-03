using Onyx.Contracts.Administration;
using Onyx.Contracts.Common;
using Onyx.Domain.Common;

namespace Onyx.Application.Identity;

/// <summary>Admin user management. Every operation applies <see cref="PermissionGrantPolicy"/> and protects the last active SuperAdmin.</summary>
public interface IUserAdministrationService
{
    Task<PagedResult<UserSummaryResponse>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<Result<UserSummaryResponse>> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<UserSummaryResponse>> InviteAsync(InviteUserRequest request, CancellationToken cancellationToken);

    Task<Result> ResendInvitationAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<UserSummaryResponse>> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<Result<UserSummaryResponse>> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken);

    Task<Result> DisableAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> EnableAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> ResetTwoFactorAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result> RevokeSessionsAsync(Guid userId, CancellationToken cancellationToken);
}
