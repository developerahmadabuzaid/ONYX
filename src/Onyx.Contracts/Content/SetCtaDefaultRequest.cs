using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="ContentType"/> null sets the global default; <see cref="CallToActionId"/> null clears the default.</summary>
public sealed record SetCtaDefaultRequest(string? ContentType, Guid? CallToActionId);
