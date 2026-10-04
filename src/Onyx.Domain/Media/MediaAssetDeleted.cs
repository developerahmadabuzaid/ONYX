using Onyx.Domain.Common;

namespace Onyx.Domain.Media;

/// <summary>
/// A media asset's database record was deleted. Stored in the outbox with the deletion, so removing the physical file is durable
/// and retried until it succeeds (the file is never left untracked).
/// </summary>
public sealed record MediaAssetDeleted(Guid MediaAssetId, string StorageKey) : IDomainEvent;
