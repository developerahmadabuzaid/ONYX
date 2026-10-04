using Onyx.Contracts.Organization;

namespace Onyx.Application.Seo;

public interface IOrganizationProfileService
{
    /// <summary>Null unless the owner switched Organization data on and every required fact is present and verified.</summary>
    Task<PublicOrganizationResponse?> GetVerifiedAsync(CancellationToken cancellationToken);
}
