using Onyx.Contracts.Common;
using Onyx.Contracts.Delivery;

namespace Onyx.Web.Areas.Admin.Delivery;

/// <summary>A project board with the active filters.</summary>
public sealed record BoardPage(BoardResponse Board, BoardFilter Filter);
