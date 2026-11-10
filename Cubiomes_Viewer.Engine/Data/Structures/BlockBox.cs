namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>A box of blocks; both corners belong to it.</summary>
public readonly record struct BlockBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    public int SizeX => MaxX - MinX + 1;
    public int SizeY => MaxY - MinY + 1;
    public int SizeZ => MaxZ - MinZ + 1;
}
