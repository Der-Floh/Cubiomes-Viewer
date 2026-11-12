using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public interface IBiomeCatalog
{
    BiomePalette DefaultPalette { get; }

    /// <summary>Returns the resource name of a biome (for example <c>plains</c>), or <see langword="null"/> when the id is unknown in that version.</summary>
    string? GetName(McVersion version, int biome);

    /// <summary>Returns a readable name such as <c>Old Growth Pine Taiga</c>, or <see langword="null"/> when the id is unknown in that version.</summary>
    string? GetDisplayName(McVersion version, int biome);

    /// <summary>Whether a biome generates in a dimension of a version.</summary>
    bool Generates(McVersion version, Dimension dimension, int biome);

    /// <summary>The ids of the biomes that generate in a dimension of a version, in ascending order.</summary>
    IReadOnlyList<int> GetBiomes(McVersion version, Dimension dimension);
}
