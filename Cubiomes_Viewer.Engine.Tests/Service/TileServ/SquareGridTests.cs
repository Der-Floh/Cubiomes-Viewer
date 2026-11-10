using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class SquareGridTests
{
    private const int CellBlocks = 1024;

    [Fact]
    public void GetVisibleCells_ViewportAcrossTheOrigin_CoversItWithAlignedCells()
    {
        var viewport = new MapViewport(new MapCamera(-100, 300, 2), 1000, 600);

        var cells = SquareGrid.GetVisibleCells(CellBlocks, viewport, maxCells: 64);

        Assert.Equal(
            [new BlockArea(-1024, 0, CellBlocks, CellBlocks), new BlockArea(0, 0, CellBlocks, CellBlocks), new BlockArea(-1024, -1024, CellBlocks, CellBlocks),
             new BlockArea(0, -1024, CellBlocks, CellBlocks), new BlockArea(-2048, 0, CellBlocks, CellBlocks), new BlockArea(-2048, -1024, CellBlocks, CellBlocks)],
            cells);
    }

    [Fact]
    public void GetVisibleCells_ViewportInsideOneCell_ReturnsThatCell()
    {
        var viewport = new MapViewport(new MapCamera(-1500, -1500, 1), 100, 100);

        Assert.Equal([new BlockArea(-2048, -2048, CellBlocks, CellBlocks)], SquareGrid.GetVisibleCells(CellBlocks, viewport, maxCells: 4));
    }

    [Fact]
    public void GetVisibleCells_MoreCellsThanAllowed_ReturnsNothing()
    {
        var viewport = new MapViewport(new MapCamera(0, 0, 64), 1280, 800);

        Assert.Empty(SquareGrid.GetVisibleCells(CellBlocks, viewport, maxCells: 64));
    }

    [Fact]
    public void GetVisibleCells_EmptyViewport_ReturnsNothing()
        => Assert.Empty(SquareGrid.GetVisibleCells(CellBlocks, new MapViewport(MapCamera.Default, 0, 600), maxCells: 64));
}
