using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Optimistic concurrency: the version the editor last saw.</summary>
public sealed record ContentVersionRequest(uint Version);
