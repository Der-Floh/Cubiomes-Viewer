using Cubiomes_Viewer.Engine.Data.Locate;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.LocateServ;

/// <summary>
/// Finds the structure, stronghold or biome nearest to a block, like the game's /locate: it searches outward on the compute workers
/// and stops once nothing further out can be nearer. Ties go to the smaller Z, then the smaller X.
/// </summary>
public interface ILocateService
{
    /// <summary>How far out biomes are searched, in blocks, as far as the game's /locate biome looks.</summary>
    int BiomeRange { get; }

    /// <summary>How far out structures of a type are searched, in blocks: a hundred of their regions, as the game's /locate, but at least as far as biomes.</summary>
    int GetStructureRange(StructureType type, McVersion version);

    /// <summary>The nearest structure of a type that generates within <paramref name="range"/> blocks, in the dimension it belongs to; progress reports the blocks covered.</summary>
    Task<LocateResult?> FindStructureAsync(WorldKey world, StructureType type, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>The middle of the nearest biome cell of 4 by 4 blocks at height <paramref name="y"/> within <paramref name="range"/> blocks, as the map draws it; progress reports the blocks covered.</summary>
    Task<LocateResult?> FindBiomeAsync(WorldKey world, int biome, int y, BlockPos origin, int range, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>The nearest stronghold of the world's Overworld, with its number in generation order; <see langword="null"/> where the version has none.</summary>
    Task<LocateResult?> FindStrongholdAsync(WorldKey world, BlockPos origin, CancellationToken cancellationToken = default);
}
