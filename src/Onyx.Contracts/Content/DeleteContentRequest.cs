using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>Permanent deletion. <see cref="Confirmation"/> must repeat the content id.</summary>
public sealed record DeleteContentRequest(string Confirmation, uint Version);
