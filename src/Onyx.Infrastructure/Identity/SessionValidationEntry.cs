namespace Onyx.Infrastructure.Identity;

public sealed record SessionValidationEntry(Guid UserId, bool IsUsable, string SecurityStampHash, DateTimeOffset AbsoluteExpiresAtUtc);
