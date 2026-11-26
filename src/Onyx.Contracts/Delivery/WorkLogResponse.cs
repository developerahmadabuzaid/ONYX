namespace Onyx.Contracts.Delivery;

/// <summary>Time logged on an issue.</summary>
public sealed record WorkLogResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    int Minutes,
    DateOnly WorkDate,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    bool CanDelete);
