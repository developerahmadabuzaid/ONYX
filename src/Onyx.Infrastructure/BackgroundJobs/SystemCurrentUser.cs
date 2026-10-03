using Onyx.Application.Abstractions;

namespace Onyx.Infrastructure.BackgroundJobs;

/// <summary>Background work runs as no user; audit records show a null user (system).</summary>
public sealed class SystemCurrentUser : ICurrentUser
{
    public Guid? UserId => null;

    public string? UserName => "system";

    public bool IsAuthenticated => false;
}
