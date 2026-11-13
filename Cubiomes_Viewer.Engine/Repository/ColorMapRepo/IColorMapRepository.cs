using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.ColorMapRepo;

/// <summary>Keeps the user's biome colour maps and reads and writes the files they are shared as.</summary>
public interface IColorMapRepository
{
    /// <summary>Loads the stored maps; nothing when none are stored.</summary>
    /// <exception cref="UnreadableDataException">The stored maps cannot be read, or a newer version wrote them.</exception>
    Task<IReadOnlyList<BiomeColorMap>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<BiomeColorMap> maps, CancellationToken cancellationToken = default);

    /// <summary>Writes maps as a file to share.</summary>
    byte[] Serialize(IReadOnlyList<BiomeColorMap> maps);

    /// <summary>Reads the maps of a shared file.</summary>
    /// <exception cref="FormatException">The content is no colour map file, or one this version cannot read.</exception>
    IReadOnlyList<BiomeColorMap> Deserialize(ReadOnlySpan<byte> content);
}
