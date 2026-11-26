namespace Onyx.Contracts.Delivery;

/// <summary>Completes or cancels a sprint; unfinished issues move to MoveOpenIssuesToSprintId (another open sprint) or, when null, to the backlog.</summary>
public sealed record CloseSprintRequest(
    Guid? MoveOpenIssuesToSprintId,
    uint Version);
