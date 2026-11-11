using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>The ways a block position is written for the user and the clipboard.</summary>
public static class BlockPosText
{
    private const int ChunkShift = 4;
    private const int RegionShift = 9;

    public static string ToCoordinates(BlockPos position)
        => string.Create(CultureInfo.InvariantCulture, $"{position.X}, {position.Z}");

    public static string ToChunk(BlockPos position)
        => string.Create(CultureInfo.InvariantCulture, $"{position.X >> ChunkShift}, {position.Z >> ChunkShift}");

    /// <summary>The block and its chunk, as shown where a position is described to the user.</summary>
    public static string ToLocation(BlockPos position)
        => string.Create(CultureInfo.InvariantCulture, $"X {position.X}, Z {position.Z}  ·  Chunk {ToChunk(position)}");

    /// <summary>The name of the region file that stores the block.</summary>
    public static string ToRegionFile(BlockPos position)
        => string.Create(CultureInfo.InvariantCulture, $"r.{position.X >> RegionShift}.{position.Z >> RegionShift}.mca");

    /// <summary>A command that teleports the player who runs it to the block, keeping their height.</summary>
    public static string ToTeleportCommand(BlockPos position)
        => string.Create(CultureInfo.InvariantCulture, $"/tp @s {position.X} ~ {position.Z}");
}
