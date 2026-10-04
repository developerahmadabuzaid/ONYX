using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ContentEditorViewModel(ContentDetailResponse Content, string LanguageCode, ContentTranslationForm? Translation, ContentSeoForm? Seo);
