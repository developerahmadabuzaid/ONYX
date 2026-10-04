using Onyx.Domain.Common;

namespace Onyx.Application.Intelligence;

/// <summary>
/// LLM port shared by the future AI SEO assistant, content engine, news summaries and advisor (docs/architecture.md §16.5).
/// No provider is connected or approved yet, so nothing implements this; callers must handle "not configured".
/// </summary>
public interface ITextGenerationService
{
    Task<Result<TextGenerationResult>> GenerateAsync(TextGenerationRequest request, CancellationToken cancellationToken);
}
