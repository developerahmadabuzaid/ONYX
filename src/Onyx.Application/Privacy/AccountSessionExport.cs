namespace Onyx.Application.Privacy;

public sealed record AccountSessionExport(DateTimeOffset CreatedAtUtc, DateTimeOffset LastRefreshedAtUtc, DateTimeOffset AbsoluteExpiresAtUtc, DateTimeOffset? RevokedAtUtc);
