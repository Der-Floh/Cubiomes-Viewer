using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Repository.ColorMapRepo;

/// <summary>Keeps the user's biome colour maps and reads and writes the files they are shared as.</summary>
public interface IColorMapRepository
{
    /// <summary>Loads the stored maps; nothing when none are stored or they cannot be read.</summary>
    Task<IReadOnlyList<BiomeColorMap>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<BiomeColorMap> maps, CancellationToken cancellationToken = default);

    /// <summary>Writes maps as a file to share.</summary>
    byte[] Serialize(IReadOnlyList<BiomeColorMap> maps);

    /// <summary>Reads the maps of a shared file.</summary>
    /// <exception cref="FormatException">The content is no colour map file, or one this version cannot read.</exception>
    IReadOnlyList<BiomeColorMap> Deserialize(ReadOnlySpan<byte> content);
}
