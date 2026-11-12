using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>The presets the app comes with and the ones the user saved, which are kept in app data.</summary>
public interface ISearchPresetService
{
    IReadOnlyList<SearchPreset> Examples { get; }

    /// <summary>The user's presets in the order of their names.</summary>
    IReadOnlyList<SearchPreset> UserPresets { get; }

    event EventHandler? UserPresetsChanged;

    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves a setup under a name; a preset of the same name, whatever its case, is replaced.</summary>
    /// <exception cref="ArgumentException">The name is blank or longer than <see cref="SearchPresetService.MaxNameLength"/> characters.</exception>
    Task SaveAsync(string name, SearchSetup setup, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
