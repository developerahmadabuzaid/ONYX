using Microsoft.AspNetCore.Authorization;

namespace Onyx.API.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
