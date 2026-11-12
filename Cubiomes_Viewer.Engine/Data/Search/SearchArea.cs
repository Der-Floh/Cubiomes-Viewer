using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Where a condition looks: a rectangle of blocks around its reference point, both corners included, or the circle
/// of <see cref="Radius"/> blocks inside that rectangle. The reference point is the world origin or the place the
/// condition it is relative to was found at.
/// </summary>
public readonly record struct SearchArea(int MinX, int MinZ, int MaxX, int MaxZ, int Radius = 0)
{
    /// <summary>The furthest a corner may lie from the reference point, so that chains of relative conditions stay inside the range of block coordinates.</summary>
    public const int MaxOffset = 10_000_000;

    [JsonIgnore]
    public int SizeX => MaxX - MinX + 1;

    [JsonIgnore]
    public int SizeZ => MaxZ - MinZ + 1;

    [JsonIgnore]
    public AreaShape Shape
    {
        get
        {
            if (Radius > 0)
                return AreaShape.Circle;
            return MinX == -MaxX && MinZ == -MaxZ && MaxX == MaxZ ? AreaShape.Square : AreaShape.Rectangle;
        }
    }

    /// <summary>A square of <paramref name="side"/> blocks, half of them on each side of the reference point.</summary>
    public static SearchArea Square(int side) => new(-side / 2, -side / 2, side / 2, side / 2);

    public static SearchArea Circle(int radius) => new(-radius, -radius, radius, radius, radius);

    public static SearchArea Rectangle(int x1, int z1, int x2, int z2)
        => new(Math.Min(x1, x2), Math.Min(z1, z2), Math.Max(x1, x2), Math.Max(z1, z2));

    /// <summary>The rectangle of blocks the area covers around a reference point.</summary>
    public BlockArea ToBlocks(BlockPos reference) => new(reference.X + MinX, reference.Z + MinZ, SizeX, SizeZ);

    /// <summary>Whether a block lies in the area around a reference point.</summary>
    public bool Contains(BlockPos reference, BlockPos position)
    {
        long x = (long)position.X - reference.X;
        long z = (long)position.Z - reference.Z;
        if (x < MinX || x > MaxX || z < MinZ || z > MaxZ)
            return false;

        return Radius <= 0 || x * x + z * z <= (long)Radius * Radius;
    }
}
