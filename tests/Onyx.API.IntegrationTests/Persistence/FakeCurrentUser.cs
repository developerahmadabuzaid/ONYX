using Onyx.Application.Abstractions;

namespace Onyx.API.IntegrationTests.Persistence;

public sealed class FakeCurrentUser(Guid? userId, string? userName) : ICurrentUser
{
    public Guid? UserId { get; } = userId;

    public string? UserName { get; } = userName;

    public bool IsAuthenticated => UserId is not null;
}
