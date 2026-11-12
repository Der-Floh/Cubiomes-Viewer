using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Service.ColorMapServ;

/// <summary>The user's biome colour maps: kept, shared as files and turned into the palettes tiles are painted with.</summary>
public interface IColorMapService
{
    /// <summary>The stored maps in the order of their names.</summary>
    IReadOnlyList<BiomeColorMap> Maps { get; }

    /// <summary>The colours biomes have without a map.</summary>
    BiomePalette DefaultPalette { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a map, in place of the one called <paramref name="previousName"/> or its own name when there is one.</summary>
    Task SaveAsync(BiomeColorMap map, string? previousName = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>The default palette with the colours of the map; the default palette itself without one.</summary>
    BiomePalette GetPalette(BiomeColorMap? map);

    /// <summary>Stores the maps of a shared file, renamed where their names are taken, and returns them as stored.</summary>
    /// <exception cref="FormatException">The content is no colour map file.</exception>
    Task<IReadOnlyList<BiomeColorMap>> ImportAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default);

    /// <summary>Writes a map as a file to share.</summary>
    byte[] Export(BiomeColorMap map);

    /// <summary>The wanted name, or the first of "name 2", "name 3" and so on that no stored map has.</summary>
    string GetFreeName(string wanted);
}
