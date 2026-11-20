using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Square rings of whole regions around the region of a reference point, nearest first: ring 0 is that region, and ring
/// n the regions n regions away from it along either axis. Every region lies in exactly one ring.
/// </summary>
internal readonly record struct RegionRings(BlockPos Reference, int RegionBlocks)
{
    private int CenterX => ToRegion(Reference.X);
    private int CenterZ => ToRegion(Reference.Z);

    /// <summary>The rectangles of blocks a ring covers: the region itself for ring 0, otherwise its top and bottom rows and its left and right columns.</summary>
    public IEnumerable<BlockArea> GetRing(int ring)
    {
        if (ring == 0)
        {
            yield return ToBlocks(CenterX, CenterZ, 1, 1);
            yield break;
        }

        var side = 2 * ring + 1;
        yield return ToBlocks(CenterX - ring, CenterZ - ring, side, 1);
        yield return ToBlocks(CenterX - ring, CenterZ + ring, side, 1);
        yield return ToBlocks(CenterX - ring, CenterZ - ring + 1, 1, side - 2);
        yield return ToBlocks(CenterX + ring, CenterZ - ring + 1, 1, side - 2);
    }

    /// <summary>The squared distance from the reference point to the nearest block outside rings 0 to <paramref name="ring"/>.</summary>
    public long GetSquaredReach(int ring)
    {
        var left = Reference.X - ((long)(CenterX - ring) * RegionBlocks - 1);
        var right = (long)(CenterX + ring + 1) * RegionBlocks - Reference.X;
        var top = Reference.Z - ((long)(CenterZ - ring) * RegionBlocks - 1);
        var bottom = (long)(CenterZ + ring + 1) * RegionBlocks - Reference.Z;
        var reach = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        return reach * reach;
    }

    /// <summary>Whether rings 0 to <paramref name="ring"/> reach past the world border on every side.</summary>
    public bool CoversWorld(int ring)
        => (long)(CenterX - ring) * RegionBlocks <= -TilePyramid.WorldLimitBlocks
            && (long)(CenterX + ring + 1) * RegionBlocks > TilePyramid.WorldLimitBlocks
            && (long)(CenterZ - ring) * RegionBlocks <= -TilePyramid.WorldLimitBlocks
            && (long)(CenterZ + ring + 1) * RegionBlocks > TilePyramid.WorldLimitBlocks;

    /// <summary>Whether a block lies inside the world border.</summary>
    public static bool IsInsideWorld(BlockPos position)
        => Math.Abs((long)position.X) <= TilePyramid.WorldLimitBlocks && Math.Abs((long)position.Z) <= TilePyramid.WorldLimitBlocks;

    private BlockArea ToBlocks(int regionX, int regionZ, int columns, int rows)
        => new(regionX * RegionBlocks, regionZ * RegionBlocks, columns * RegionBlocks, rows * RegionBlocks);

    private int ToRegion(int block) => (int)Math.Floor(block / (double)RegionBlocks);
}
