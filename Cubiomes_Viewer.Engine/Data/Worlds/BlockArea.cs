namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>A rectangle of blocks that includes its left and top edge and excludes its right and bottom edge.</summary>
public readonly record struct BlockArea(int X, int Z, int SizeX, int SizeZ)
{
    public int Right => X + SizeX;
    public int Bottom => Z + SizeZ;

    public bool Contains(BlockPos position)
        => position.X >= X && position.X < Right && position.Z >= Z && position.Z < Bottom;
}
