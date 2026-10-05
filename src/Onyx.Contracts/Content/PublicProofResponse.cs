using Onyx.Contracts.Common;
using Onyx.Contracts.Content;

namespace Onyx.Contracts.Content;

/// <summary>Only verified, published proof in the requested language.</summary>
public sealed record PublicProofResponse(
    IReadOnlyList<PublicProofItemResponse> Capabilities, IReadOnlyList<PublicProofItemResponse> Metrics, IReadOnlyList<PublicProofItemResponse> Testimonials);
