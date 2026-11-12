using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Repository.PresetRepo;

public interface ISearchPresetRepository
{
    /// <summary>Loads the user's presets; when none are stored or they cannot be read, the list is empty.</summary>
    Task<IReadOnlyList<SearchPreset>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<SearchPreset> presets, CancellationToken cancellationToken = default);
}
