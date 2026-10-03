namespace Onyx.Application.Abstractions;

/// <summary>The authenticated admin user for the current operation, or none for anonymous requests and background jobs.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }
}
