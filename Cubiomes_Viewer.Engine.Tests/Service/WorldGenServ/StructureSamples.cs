using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

/// <summary>Finds a few structures of a type around the world origin, for tests that need real ones of rare types too.</summary>
internal static class StructureSamples
{
    private const int FirstSide = 4096;
    private const int MaxSide = 32768;
    private const int MineshaftSide = 1024;

    /// <summary>Widens a square around the origin until it holds <paramref name="count"/> structures or reaches its widest.</summary>
    public static BlockPos[] FindSome(IWorldGenerator generator, StructureType type, int count)
    {
        if (type == StructureType.Stronghold)
            return [.. generator.FindStrongholds().Take(count)];
        if (type == StructureType.Mineshaft)
            return [.. generator.FindStructures(type, Square(MineshaftSide)).Take(count)];

        var side = FirstSide;
        var found = generator.FindStructures(type, Square(side));
        while (found.Length < count && side < MaxSide)
        {
            side *= 2;
            found = generator.FindStructures(type, Square(side));
        }

        return [.. found.Take(count)];
    }

    private static BlockArea Square(int side) => new(-side / 2, -side / 2, side, side);
}
