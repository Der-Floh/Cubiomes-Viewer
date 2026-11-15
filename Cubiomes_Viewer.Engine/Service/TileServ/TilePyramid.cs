using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>Pure tile arithmetic: which level a zoom uses and which tiles a viewport touches.</summary>
public static class TilePyramid
{
    public const int WorldLimitBlocks = 30_000_000;

    private const int MaxVisibleTiles = 2048;
    private const int VerticalBiomeCellShift = 2;

    /// <summary>Returns the finest scale whose samples are still at least <see cref="TileMetrics.MinSamplePixels"/> wide.</summary>
    public static int SelectScale(TileMetrics metrics, double blocksPerPixel)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blocksPerPixel);

        foreach (var scale in metrics.Scales)
        {
            if (scale / blocksPerPixel >= metrics.MinSamplePixels)
                return scale;
        }

        return metrics.Scales[^1];
    }

    /// <summary>The farthest zoom, in blocks per pixel, at which <see cref="SelectScale"/> still picks <paramref name="scale"/> or a finer one.</summary>
    public static double GetMaxBlocksPerPixel(TileMetrics metrics, int scale)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

        return scale / metrics.MinSamplePixels;
    }

    public static int? GetCoarserScale(TileMetrics metrics, int scale)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        for (var i = 0; i < metrics.Scales.Count - 1; i++)
        {
            if (metrics.Scales[i] == scale)
                return metrics.Scales[i + 1];
        }

        return null;
    }

    public static long GetTileBlocks(TileMetrics metrics, int scale) => (long)metrics.TileSamples * scale;

    public static TileKey GetTileAt(TileMetrics metrics, int scale, double blockX, double blockZ)
    {
        var tileBlocks = (double)GetTileBlocks(metrics, scale);
        return new TileKey(scale, (int)Math.Floor(blockX / tileBlocks), (int)Math.Floor(blockZ / tileBlocks));
    }

    /// <summary>Lists the tiles of a level that intersect the viewport, nearest to the view centre first.</summary>
    public static IReadOnlyList<TileKey> GetVisibleTiles(TileMetrics metrics, int scale, MapViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        if (viewport.IsEmpty)
            return [];

        var tileBlocks = (double)GetTileBlocks(metrics, scale);
        var minX = ToTileIndex(Math.Max(viewport.Left, -WorldLimitBlocks), tileBlocks);
        var maxX = ToTileIndex(Math.Min(viewport.Right, WorldLimitBlocks - 1), tileBlocks);
        var minZ = ToTileIndex(Math.Max(viewport.Top, -WorldLimitBlocks), tileBlocks);
        var maxZ = ToTileIndex(Math.Min(viewport.Bottom, WorldLimitBlocks - 1), tileBlocks);
        if (maxX < minX || maxZ < minZ || (long)(maxX - minX + 1) * (maxZ - minZ + 1) > MaxVisibleTiles)
            return [];

        var centerX = viewport.Camera.CenterX / tileBlocks - 0.5;
        var centerZ = viewport.Camera.CenterZ / tileBlocks - 0.5;
        List<TileKey> tiles = [];
        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
                tiles.Add(new TileKey(scale, x, z));
        }

        tiles.Sort((left, right) => DistanceSquared(left, centerX, centerZ).CompareTo(DistanceSquared(right, centerX, centerZ)));
        return tiles;
    }

    /// <summary>
    /// The generator area of a tile, widened by <paramref name="border"/> samples on each side. <paramref name="blockY"/> is in
    /// blocks; coarser scales sample the vertical axis in cells of 4 blocks.
    /// </summary>
    public static BiomeArea GetArea(TileKey tile, int samples, int blockY, int border = 0)
        => new(tile.Scale, tile.X * samples - border, tile.Z * samples - border, samples + 2 * border, samples + 2 * border, tile.Scale == 1 ? blockY : blockY >> VerticalBiomeCellShift);

    /// <summary>
    /// The tile of another pyramid that covers <paramref name="tile"/> whole with samples no finer than its own, each
    /// spanning a whole number of its samples: the one at the finest such scale, or <see langword="null"/> when none of
    /// the scales of <paramref name="covering"/> does.
    /// </summary>
    public static TileKey? FindCoveringTile(TileMetrics metrics, TileKey tile, TileMetrics covering)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(covering);

        var tileBlocks = GetTileBlocks(metrics, tile.Scale);
        foreach (var scale in covering.Scales)
        {
            var coveringBlocks = GetTileBlocks(covering, scale);
            if (scale % tile.Scale == 0 && tileBlocks % scale == 0 && coveringBlocks % tileBlocks == 0)
                return new TileKey(scale, (int)FloorDivide(tile.X * tileBlocks, coveringBlocks), (int)FloorDivide(tile.Z * tileBlocks, coveringBlocks));
        }

        return null;
    }

    /// <summary>Where the samples of <paramref name="tile"/> lie among those of a tile that covers it whole, as <see cref="FindCoveringTile"/> finds one.</summary>
    public static CoveredSamples GetCoveredSamples(TileKey tile, int samples, TileKey covering, int coveringSamples)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samples);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(coveringSamples);

        var tileBlocks = (long)samples * tile.Scale;
        var coveringBlocks = (long)coveringSamples * covering.Scale;
        var left = tile.X * tileBlocks - covering.X * coveringBlocks;
        var top = tile.Z * tileBlocks - covering.Z * coveringBlocks;
        var isCovered = covering.Scale % tile.Scale == 0 && tileBlocks % covering.Scale == 0
            && left >= 0 && top >= 0 && left + tileBlocks <= coveringBlocks && top + tileBlocks <= coveringBlocks
            && left % covering.Scale == 0 && top % covering.Scale == 0;
        if (!isCovered)
            throw new ArgumentException($"The tile {covering} of {coveringSamples} samples does not cover the tile {tile} of {samples} samples in whole samples.", nameof(covering));

        return new CoveredSamples((int)(left / covering.Scale), (int)(top / covering.Scale), (int)(tileBlocks / covering.Scale));
    }

    private static int ToTileIndex(double block, double tileBlocks) => (int)Math.Floor(block / tileBlocks);

    private static long FloorDivide(long value, long divisor) => value >= 0 ? value / divisor : (value - divisor + 1) / divisor;

    private static double DistanceSquared(TileKey tile, double centerX, double centerZ)
        => (tile.X - centerX) * (tile.X - centerX) + (tile.Z - centerZ) * (tile.Z - centerZ);
}
