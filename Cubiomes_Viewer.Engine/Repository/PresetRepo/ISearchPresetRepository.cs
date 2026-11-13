using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Repository.PresetRepo;

public interface ISearchPresetRepository
{
    /// <summary>Loads the user's presets; when none are stored, the list is empty.</summary>
    /// <exception cref="UnreadableDataException">The stored presets cannot be read, or a newer version wrote them.</exception>
    Task<IReadOnlyList<SearchPreset>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<SearchPreset> presets, CancellationToken cancellationToken = default);
}
