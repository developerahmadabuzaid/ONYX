using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Messaging;

/// <summary>
/// <see cref="Placeholders"/> may be used as <c>{name}</c> in the wording; <see cref="RequiredPlaceholders"/> must appear. Links are
/// code-owned and cannot be edited.
/// </summary>
public sealed record EmailTemplateResponse(
    string Key, IReadOnlyList<string> Placeholders, IReadOnlyList<string> RequiredPlaceholders, bool HasAction, IReadOnlyList<EmailTemplateCopyResponse> Copies);
