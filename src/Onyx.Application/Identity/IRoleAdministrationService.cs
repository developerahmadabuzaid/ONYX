using Onyx.Contracts.Administration;
using Onyx.Domain.Common;

namespace Onyx.Application.Identity;

public interface IRoleAdministrationService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken cancellationToken);

    Task<Result<RoleResponse>> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken);

    Task<Result<RoleResponse>> UpdateAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken);

    /// <summary>Only roles with no assigned users can be deleted; SuperAdmin never.</summary>
    Task<Result> DeleteAsync(Guid roleId, CancellationToken cancellationToken);
}
