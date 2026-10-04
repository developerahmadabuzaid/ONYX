using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Cms;

public sealed record BlockTextResult(IReadOnlyList<ContentBlockDto> Blocks, IReadOnlyList<BlockTextError> Errors);
