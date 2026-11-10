namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// A horizontal area of biome samples. <see cref="X"/>, <see cref="Z"/> and the sizes are in units
/// of <see cref="Scale"/> blocks; <see cref="Y"/> is in blocks at scale 1 and in units of 4 blocks otherwise.
/// </summary>
public readonly record struct BiomeArea(int Scale, int X, int Z, int SizeX, int SizeZ, int Y)
{
    public int SampleCount => SizeX * SizeZ;
}
