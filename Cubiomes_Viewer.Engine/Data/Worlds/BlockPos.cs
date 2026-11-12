using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// A horizontal block position. Laid out as two consecutive 32-bit integers,
/// so the native generator can fill spans of positions directly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct BlockPos(int X, int Z)
{
    /// <summary>The straight-line distance to another block, in blocks.</summary>
    public double DistanceTo(BlockPos other) => double.Hypot((double)other.X - X, (double)other.Z - Z);

    /// <summary>The square of the distance to another block, exact for every pair of positions.</summary>
    public long SquaredDistanceTo(BlockPos other)
    {
        long x = (long)other.X - X;
        long z = (long)other.Z - Z;
        return x * x + z * z;
    }

    /// <summary>The middle of some positions, rounded towards zero.</summary>
    public static BlockPos MiddleOf(ReadOnlySpan<BlockPos> positions)
    {
        if (positions.IsEmpty)
            throw new ArgumentException("The middle of no positions is undefined.", nameof(positions));

        long sumX = 0, sumZ = 0;
        foreach (var position in positions)
        {
            sumX += position.X;
            sumZ += position.Z;
        }

        return new BlockPos((int)(sumX / positions.Length), (int)(sumZ / positions.Length));
    }
}
