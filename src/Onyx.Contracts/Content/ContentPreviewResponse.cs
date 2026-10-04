using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>A draft preview: never indexable, never cached, and carries no admin identity.</summary>
public sealed record ContentPreviewResponse(PublicContentResponse Content, SeoPreviewResponse SeoPreview, DateTimeOffset ExpiresAtUtc);
