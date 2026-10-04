using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary>The raw token is returned once and never stored; only its hash is kept.</summary>
public sealed record PreviewLinkResponse(string Token, string LanguageCode, DateTimeOffset ExpiresAtUtc);
