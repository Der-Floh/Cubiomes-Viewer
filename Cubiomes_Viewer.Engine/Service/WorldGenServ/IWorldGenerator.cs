using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Loot;
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

    /// <summary>How the structures of the generator's worlds generate.</summary>
    StructureRules Rules { get; }

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

    /// <summary>Whether <see cref="GenerateSurface"/> can read the applied dimension: the Overworld from 1.14 and the End from 1.9.</summary>
    bool HasExactTerrain { get; }

    /// <summary>
    /// Fills <paramref name="destination"/> row by row with the Y of the lowest air block above the highest solid block of each
    /// column, as the terrain noise shapes it before water, caves and anything on the ground. The area is in blocks, at most
    /// <see cref="Constants.MaxSurfaceSide"/> a side, and <see cref="HasExactTerrain"/> must hold.
    /// </summary>
    void GenerateSurface(BlockArea area, Span<short> destination);

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
    /// Describes a structure found by <see cref="FindStructures"/>, or a stronghold: its variant and, for end cities, fortresses,
    /// strongholds and mineshafts, its pieces. The dimension of the structure type must be applied.
    /// </summary>
    StructureDetails DescribeStructure(StructureType type, BlockPos position);

    /// <summary>
    /// The Y a structure found by <see cref="FindStructures"/> is built at where the terrain tells it exactly: end cities, nether
    /// fossils and, from 1.14, woodland mansions. <see langword="null"/> for other types, or when the terrain keeps the structure
    /// from generating. The dimension of the structure type must be applied.
    /// </summary>
    int? FindStructureFloor(StructureType type, BlockPos position);

    /// <summary>
    /// Rolls what the containers of a structure found by <see cref="FindStructures"/>, or of a stronghold, hold when they are first
    /// opened. <see langword="null"/> when the generator cannot tell the loot of the type in the version, or of this structure.
    /// The dimension of the structure type must be applied.
    /// </summary>
    StructureLoot? FindLoot(StructureType type, BlockPos position);

    /// <summary>
    /// Counts the items a query asks for in the containers of a structure, as <see cref="FindLoot"/> would roll them, without
    /// listing them. <see langword="null"/> where <see cref="FindLoot"/> is.
    /// </summary>
    LootCount? CountLoot(StructureType type, BlockPos position, LootQuery query);

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
