namespace Onyx.Contracts.Delivery;

/// <summary>A "blocks" link: <see cref="BlockerId"/> has to finish before <see cref="BlockedId"/>.</summary>
public sealed record TimelineDependencyResponse(Guid BlockerId, Guid BlockedId);
