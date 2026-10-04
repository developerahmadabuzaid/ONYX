using Onyx.Contracts.Content;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record CtaListViewModel(IReadOnlyList<CtaResponse> Ctas, CtaDefaultsResponse Defaults);
