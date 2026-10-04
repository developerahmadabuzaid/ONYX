using Onyx.Contracts.Content;

namespace Onyx.Web.Content;

public sealed record PreviewViewModel(PageViewModel Page, SeoPreviewResponse SeoPreview, DateTimeOffset ExpiresAtUtc);
