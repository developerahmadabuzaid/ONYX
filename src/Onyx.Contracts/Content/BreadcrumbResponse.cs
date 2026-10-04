using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Content;

/// <summary><see cref="Path"/> is the full site path including the language, e.g. <c>/en/services</c> (unencoded).</summary>
public sealed record BreadcrumbResponse(string Title, string Path);
