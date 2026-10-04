using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

public sealed record UpdateContentTranslationRequest(
    string Title, string? Summary, string Slug, IReadOnlyList<ContentBlockDto> Body, IReadOnlyList<FaqDto> Faqs, uint Version);
