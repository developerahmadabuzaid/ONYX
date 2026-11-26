using Onyx.Application.Delivery;
using Onyx.Contracts.Delivery;
using Onyx.Domain.Delivery;

namespace Onyx.Application.Tests.Delivery;

public sealed class IssueRankingTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void An_empty_list_or_no_anchor_places_last()
    {
        Assert.Equal((IssueRanking.Spacing, false), IssueRanking.Place([], null));
        Assert.Equal((3072L, false), IssueRanking.Place([new(A, 1024), new(B, 2048)], null));
    }

    [Fact]
    public void Placing_before_an_issue_takes_the_midpoint_or_goes_first()
    {
        IReadOnlyList<IssueRankSlot> list = [new(A, 1024), new(B, 2048), new(C, 3072)];
        Assert.Equal((1536L, false), IssueRanking.Place(list, B));
        Assert.Equal((0L, false), IssueRanking.Place(list, A));
    }

    [Fact]
    public void Adjacent_neighbours_ask_for_a_renumber_and_unknown_anchors_are_rejected()
    {
        Assert.Equal((null, false), IssueRanking.Place([new(A, 10), new(B, 11)], B));
        Assert.Equal((null, true), IssueRanking.Place([new(A, 10)], C));
    }

    [Theory]
    [InlineData(null, false, false, false, false, false)]
    [InlineData(ProjectRole.Viewer, true, false, false, false, false)]
    [InlineData(ProjectRole.Contributor, true, true, false, false, false)]
    [InlineData(ProjectRole.Developer, true, true, true, false, false)]
    [InlineData(ProjectRole.Manager, true, true, true, true, false)]
    [InlineData(ProjectRole.Admin, true, true, true, true, true)]
    public void Project_roles_grant_increasing_capabilities(ProjectRole? role, bool view, bool create, bool edit, bool sprints, bool manage)
    {
        var can = DeliveryAccess.Capabilities(new DeliveryViewer(Guid.NewGuid(), IsAdministrator: false), role);
        Assert.Equal(new DeliveryCapabilities(view, create, create, edit, sprints, manage), can);
    }

    [Fact]
    public void Workspace_administrators_can_do_everything_without_membership() =>
        Assert.Equal(new DeliveryCapabilities(true, true, true, true, true, true), DeliveryAccess.Capabilities(new DeliveryViewer(Guid.NewGuid(), true), null));
}
