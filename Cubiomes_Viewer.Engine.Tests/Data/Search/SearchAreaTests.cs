using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Data.Search;

public sealed class SearchAreaTests
{
    private static readonly BlockPos _reference = new(1000, -2000);

    [Fact]
    public void Square_ReachesHalfItsSideOnEachSide()
    {
        var area = SearchArea.Square(1024);

        Assert.Equal(new SearchArea(-512, -512, 512, 512), area);
        Assert.Equal(AreaShape.Square, area.Shape);
        Assert.Equal(1025, area.SizeX);
    }

    [Fact]
    public void Rectangle_CornersInAnyOrder_AreSorted()
    {
        var area = SearchArea.Rectangle(300, -50, -100, -400);

        Assert.Equal(new SearchArea(-100, -400, 300, -50), area);
        Assert.Equal(AreaShape.Rectangle, area.Shape);
    }

    [Fact]
    public void ToBlocks_IncludesBothCorners()
    {
        var blocks = SearchArea.Rectangle(-10, -20, 30, 40).ToBlocks(_reference);

        Assert.Equal(new BlockArea(990, -2020, 41, 61), blocks);
        Assert.True(blocks.Contains(new BlockPos(1030, -1960)));
    }

    [Theory]
    [InlineData(-10, -20, true)]
    [InlineData(30, 40, true)]
    [InlineData(31, 0, false)]
    [InlineData(0, -21, false)]
    public void Contains_Rectangle_IncludesItsCorners(int offsetX, int offsetZ, bool expected)
    {
        var area = SearchArea.Rectangle(-10, -20, 30, 40);

        Assert.Equal(expected, area.Contains(_reference, new BlockPos(_reference.X + offsetX, _reference.Z + offsetZ)));
    }

    [Theory]
    [InlineData(100, 0, true)]
    [InlineData(60, 80, true)]
    [InlineData(71, 71, false)]
    [InlineData(100, 1, false)]
    public void Contains_Circle_IncludesOnlyBlocksWithinItsRadius(int offsetX, int offsetZ, bool expected)
    {
        var area = SearchArea.Circle(100);

        Assert.Equal(AreaShape.Circle, area.Shape);
        Assert.Equal(expected, area.Contains(_reference, new BlockPos(_reference.X + offsetX, _reference.Z + offsetZ)));
    }

    [Fact]
    public void Contains_PositionsAtBothEndsOfTheWorld_DoesNotOverflow()
    {
        var area = SearchArea.Circle(SearchArea.MaxOffset);

        Assert.False(area.Contains(new BlockPos(int.MinValue, int.MinValue), new BlockPos(int.MaxValue, int.MaxValue)));
    }
}
