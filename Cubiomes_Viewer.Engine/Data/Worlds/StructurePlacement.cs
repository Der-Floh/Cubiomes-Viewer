namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// Where a structure type generates in one Minecraft version: its dimension and the side length of the
/// square regions that each hold at most one generation attempt.
/// </summary>
public readonly record struct StructurePlacement(Dimension Dimension, int RegionBlocks)
{
    /// <summary>The number of regions an area touches, which is the most attempts it can hold.</summary>
    public long CountRegions(BlockArea area)
        => (long)(ToRegion(area.Right - 1) - ToRegion(area.X) + 1) * (ToRegion(area.Bottom - 1) - ToRegion(area.Z) + 1);

    private int ToRegion(int block) => (int)Math.Floor(block / (double)RegionBlocks);
}
