using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Messaging;

public sealed record SaveEmailTemplateCopyRequest(string Subject, string Heading, IReadOnlyList<string> Paragraphs, string? ActionLabel, string? Footer, uint? Version);
