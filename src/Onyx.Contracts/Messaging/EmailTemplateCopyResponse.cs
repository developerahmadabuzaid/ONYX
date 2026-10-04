using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Messaging;

/// <summary>The effective wording for one language: the admin override when <see cref="IsCustomized"/>, otherwise the code default.</summary>
public sealed record EmailTemplateCopyResponse(
    string LanguageCode, bool IsCustomized, string Subject, string Heading, IReadOnlyList<string> Paragraphs, string? ActionLabel, string? Footer, uint? Version);
