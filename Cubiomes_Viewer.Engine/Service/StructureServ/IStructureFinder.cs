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
    /// Gets the block position at which a structure of <paramref name="type"/> attempts to generate in a region.
    /// Returns <see langword="false"/> when the region holds no attempt or the version has no such structure.
    /// The position depends only on the lower 48 bits of <paramref name="seed"/> and ignores biomes.
    /// </summary>
    bool TryGetRegionPosition(StructureType type, McVersion version, long seed, int regionX, int regionZ, out BlockPos position);

    /// <summary>
    /// Gets the ids of the biomes a structure type can generate in. Returns <see langword="false"/> when the generator
    /// has no biome rule for the type.
    /// </summary>
    bool TryGetViableBiomes(StructureType type, McVersion version, [NotNullWhen(true)] out IReadOnlySet<int>? biomes);
}
