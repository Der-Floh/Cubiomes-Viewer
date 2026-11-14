using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Data;

/// <summary>How the Overworld and the Nether line up: each block of the Nether stands for eight by eight blocks of the Overworld.</summary>
public static class NetherMath
{
    public const int Scale = 8;

    /// <summary>The Nether block over an Overworld block.</summary>
    public static BlockPos ToNether(BlockPos overworld) => new(FloorDivide(overworld.X), FloorDivide(overworld.Z));

    /// <summary>The middle of the Overworld blocks a Nether block stands for, where the game sends a player standing in the middle of it; kept inside the world border.</summary>
    public static BlockPos ToOverworld(BlockPos nether) => new(ToOverworld(nether.X), ToOverworld(nether.Z));

    private static int FloorDivide(int coordinate) => (int)Math.Floor(coordinate / (double)Scale);

    private static int ToOverworld(int coordinate)
        => (int)Math.Clamp((long)coordinate * Scale + Scale / 2, -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks);
}
