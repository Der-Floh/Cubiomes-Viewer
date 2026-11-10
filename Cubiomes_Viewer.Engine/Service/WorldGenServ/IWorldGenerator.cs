using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

/// <summary>
/// A biome generator for one Minecraft version and flag set. Instances are not thread-safe:
/// each worker owns its own generator and applies a seed before generating.
/// </summary>
public interface IWorldGenerator : IDisposable
{
    McVersion Version { get; }
    GeneratorFlags Flags { get; }

    void Apply(Dimension dimension, long seed);

    /// <summary>Returns the biome id at a scaled position, or <c>-1</c> when no biome exists there.</summary>
    int GetBiomeAt(int scale, int x, int y, int z);

    /// <summary>
    /// Fills <paramref name="destination"/> row by row (index <c>z * SizeX + x</c>) with one biome id per sample;
    /// samples without a biome are written as <c>255</c>.
    /// </summary>
    void GenerateBiomes(BiomeArea area, Span<byte> destination);

    /// <summary>
    /// Whether a structure can generate at a position its type attempts to generate at.
    /// The dimension of the structure type must be applied.
    /// </summary>
    bool CanGenerateStructure(StructureType type, BlockPos position);

    /// <summary>
    /// Finds the structures of one type that generate inside an area, region by region.
    /// The dimension of the structure type must be applied.
    /// </summary>
    BlockPos[] FindStructures(StructureType type, BlockArea area);

    /// <summary>
    /// Finds the world spawn point, or <see langword="null"/> for versions whose spawn cannot be predicted.
    /// The Overworld must be applied.
    /// </summary>
    BlockPos? FindSpawn();

    /// <summary>Finds all strongholds in generation order. The Overworld must be applied.</summary>
    BlockPos[] FindStrongholds();
}
