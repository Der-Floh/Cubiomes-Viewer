using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>A grid of equal squares of blocks that starts at the world origin.</summary>
public static class SquareGrid
{
    /// <summary>
    /// Lists the squares that intersect the viewport, nearest to the view centre first.
    /// Returns nothing when more than <paramref name="maxCells"/> squares would be visible.
    /// </summary>
    public static IReadOnlyList<BlockArea> GetVisibleCells(int cellBlocks, MapViewport viewport, int maxCells)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellBlocks);
        if (viewport.IsEmpty)
            return [];

        var minX = ToCellIndex(Math.Max(viewport.Left, -TilePyramid.WorldLimitBlocks), cellBlocks);
        var maxX = ToCellIndex(Math.Min(viewport.Right, TilePyramid.WorldLimitBlocks - 1), cellBlocks);
        var minZ = ToCellIndex(Math.Max(viewport.Top, -TilePyramid.WorldLimitBlocks), cellBlocks);
        var maxZ = ToCellIndex(Math.Min(viewport.Bottom, TilePyramid.WorldLimitBlocks - 1), cellBlocks);
        if (maxX < minX || maxZ < minZ || (long)(maxX - minX + 1) * (maxZ - minZ + 1) > maxCells)
            return [];

        List<BlockArea> cells = [];
        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
                cells.Add(new BlockArea(x * cellBlocks, z * cellBlocks, cellBlocks, cellBlocks));
        }

        var camera = viewport.Camera;
        cells.Sort((left, right) => DistanceSquared(left, camera).CompareTo(DistanceSquared(right, camera)));
        return cells;
    }

    private static int ToCellIndex(double block, int cellBlocks) => (int)Math.Floor(block / cellBlocks);

    private static double DistanceSquared(BlockArea cell, MapCamera camera)
    {
        var deltaX = cell.X + cell.SizeX / 2.0 - camera.CenterX;
        var deltaZ = cell.Z + cell.SizeZ / 2.0 - camera.CenterZ;
        return deltaX * deltaX + deltaZ * deltaZ;
    }
}
