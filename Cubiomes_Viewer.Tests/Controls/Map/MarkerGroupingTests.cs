using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Tests.Controls.Map;

public sealed class MarkerGroupingTests
{
    [Fact]
    public void Group_PositionsFartherApartThanTheReach_StayApart()
    {
        BlockPos[] positions = [new(0, 0), new(100, 0), new(0, 100)];

        var groups = MarkerGrouping.Group(positions, reach: 100);

        Assert.Equal(3, groups.Length);
        Assert.All(groups, group => Assert.True(group.IsSingle));
        Assert.Equal(positions.OrderBy(position => position.Z).ThenBy(position => position.X), groups.Select(group => positions[group.First]));
    }

    [Fact]
    public void Group_PositionsWithinReach_BecomeOneMarkerInTheirMiddleWithTheirBounds()
    {
        BlockPos[] positions = [new(10, 10), new(40, 30), new(30, 60), new(500, 500)];

        var groups = MarkerGrouping.Group(positions, reach: 64);

        var group = Assert.Single(groups, group => !group.IsSingle);
        Assert.Equal(3, group.Count);
        Assert.Equal((80 / 3.0, 100 / 3.0), (group.X, group.Z));
        Assert.Equal((new BlockPos(10, 10), new BlockPos(40, 60)), (group.Min, group.Max));
        Assert.Equal((500, 500, 1), (groups[^1].X, groups[^1].Z, groups[^1].Count));
    }

    [Fact]
    public void Group_SameReachInAnotherOrder_GivesTheSameGroups()
    {
        BlockPos[] positions = [new(0, 0), new(50, 10), new(90, 20), new(130, 30), new(-40, -40), new(1000, 0)];
        var shuffled = positions.Reverse().ToArray();

        var groups = MarkerGrouping.Group(positions, reach: 60);
        var again = MarkerGrouping.Group(shuffled, reach: 60);

        Assert.Equal(groups.Select(group => (group.X, group.Z, group.Count)), again.Select(group => (group.X, group.Z, group.Count)));
    }

    [Fact]
    public void Group_ReachMeasuredAlongBothAxes_GroupsOnlyWhereSquareMarkersOverlap()
    {
        BlockPos[] diagonal = [new(0, 0), new(40, 40)];
        BlockPos[] apartOnOneAxis = [new(0, 0), new(60, 10)];

        Assert.Single(MarkerGrouping.Group(diagonal, reach: 50));
        Assert.Equal(2, MarkerGrouping.Group(apartOnOneAxis, reach: 50).Length);
    }

    [Fact]
    public void Group_NoReach_KeepsEveryPositionOnItsOwn()
    {
        BlockPos[] positions = [new(0, 0), new(0, 0), new(1, 1)];

        Assert.Equal(3, MarkerGrouping.Group(positions, reach: 0).Length);
    }
}
