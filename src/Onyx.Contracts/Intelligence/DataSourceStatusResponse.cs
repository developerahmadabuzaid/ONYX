using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

/// <summary><see cref="Status"/> is the provider connection status (NotConfigured until credentials exist and a test passes).</summary>
public sealed record DataSourceStatusResponse(string Source, string ProviderKey, string Status, string Purpose);
