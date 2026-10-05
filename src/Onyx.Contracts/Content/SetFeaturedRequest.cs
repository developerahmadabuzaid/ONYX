using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

public sealed record SetFeaturedRequest(bool Featured, uint Version);
