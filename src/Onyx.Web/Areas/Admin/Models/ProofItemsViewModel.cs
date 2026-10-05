using Onyx.Contracts.Content;
using Onyx.Contracts.Proof;

namespace Onyx.Web.Areas.Admin.Models;

public sealed record ProofItemsViewModel(ContentDetailResponse Content, IReadOnlyList<ProofItemResponse> Items);
