using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
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
    /// Fills <paramref name="destination"/> row by row with one value per sample: a climate parameter times
    /// <see cref="Constants.ClimateUnit"/>, as the biome lookup sees it. With <paramref name="octaves"/> above zero
    /// only that many of the widest noise octaves contribute. The Overworld must be applied.
    /// </summary>
    void GenerateClimate(ClimateParameter parameter, int octaves, BiomeArea area, Span<short> destination);

    /// <summary>
    /// Fills <paramref name="destination"/> row by row with the biome ids of an inner layer of the layered generator;
    /// samples the layer leaves empty are written as <c>255</c>. The Overworld must be applied.
    /// </summary>
    void GenerateLayer(GeneratorLayer layer, BiomeArea area, Span<byte> destination);

    /// <summary>
    /// Fills <paramref name="destination"/> row by row with the approximate surface height in blocks times
    /// <see cref="Constants.HeightUnit"/>. The Overworld or the End must be applied.
    /// </summary>
    void GenerateHeights(BiomeArea area, Span<short> destination);

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
    /// Like <see cref="FindStructures(StructureType, BlockArea)"/>, but writes the positions into
    /// <paramref name="destination"/>, which must hold one position per region the area touches, and returns their number.
    /// </summary>
    int FindStructures(StructureType type, BlockArea area, Span<BlockPos> destination);

    /// <summary>
    /// Describes a structure found by <see cref="FindStructures"/>: its variant and, for end cities and fortresses, its pieces.
    /// The dimension of the structure type must be applied.
    /// </summary>
    StructureDetails DescribeStructure(StructureType type, BlockPos position);

    /// <summary>
    /// Finds the gateways around the main End island and where they lead, in the order they appear.
    /// Returns nothing for versions whose gateways cannot be followed. The End must be applied.
    /// </summary>
    EndGateway[] FindEndGateways();

    /// <summary>
    /// Finds the world spawn point, or <see langword="null"/> for versions whose spawn cannot be predicted.
    /// The Overworld must be applied.
    /// </summary>
    BlockPos? FindSpawn();

    /// <summary>Finds all strongholds in generation order. The Overworld must be applied.</summary>
    BlockPos[] FindStrongholds();

    /// <summary>
    /// Finds the first strongholds in generation order, as many as <paramref name="destination"/> holds, and returns
    /// their number. The Overworld must be applied.
    /// </summary>
    int FindStrongholds(Span<BlockPos> destination);
}
