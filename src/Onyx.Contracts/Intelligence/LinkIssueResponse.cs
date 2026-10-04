using Onyx.Contracts.Common;
using Onyx.Contracts.Content;
using Onyx.Contracts.Intelligence;

namespace Onyx.Contracts.Intelligence;

public sealed record LinkIssueResponse(Guid ContentItemId, string LanguageCode, string AnchorText, string Target, string Problem);
