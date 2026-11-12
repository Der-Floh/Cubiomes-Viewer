using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public interface IStructureFinder
{
    /// <summary>
    /// Gets how a structure type is placed in a version.
    /// Returns <see langword="false"/> when the version has no such structure.
    /// </summary>
    bool TryGetPlacement(StructureType type, McVersion version, out StructurePlacement placement);

    /// <summary>
    /// Gets the salt the game places a structure type with in a version. Returns <see langword="false"/> when the
    /// version has no such structure or its positions do not depend on a salt, as for mineshafts.
    /// </summary>
    bool TryGetGameSalt(StructureType type, McVersion version, out int salt);

    /// <summary>
    /// Gets the block position at which a structure of <paramref name="type"/> attempts to generate in a region.
    /// Returns <see langword="false"/> when the region holds no attempt or the version has no such structure.
    /// The position ignores biomes. It depends only on the lower 48 bits of <paramref name="seed"/>, except for the
    /// desert wells, geodes and End gateways of 1.18 and later, which use all of it.
    /// </summary>
    bool TryGetRegionPosition(StructureType type, McVersion version, StructureRules rules, long seed, int regionX, int regionZ, out BlockPos position);

    /// <summary>
    /// Writes the positions at which structures of a type attempt to generate inside an area and returns their number;
    /// <paramref name="destination"/> must hold one position per region the area touches. The attempts ignore biomes,
    /// and every structure of the world is at one of them.
    /// </summary>
    int FindAttempts(StructureType type, McVersion version, StructureRules rules, long seed, BlockArea area, Span<BlockPos> destination);

    /// <summary>
    /// Gets the ids of the biomes a structure type can generate in. Returns <see langword="false"/> when the generator
    /// has no biome rule for the type.
    /// </summary>
    bool TryGetViableBiomes(StructureType type, McVersion version, [NotNullWhen(true)] out IReadOnlySet<int>? biomes);
}
