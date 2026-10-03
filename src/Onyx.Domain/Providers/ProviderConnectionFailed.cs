using Onyx.Domain.Common;

namespace Onyx.Domain.Providers;

/// <summary>Raised only on the transition into <see cref="ProviderConnectionStatus.Error"/>, so repeated failures do not flood notifications.</summary>
public sealed record ProviderConnectionFailed(Guid ProviderConnectionId, string ProviderKey) : IDomainEvent;
