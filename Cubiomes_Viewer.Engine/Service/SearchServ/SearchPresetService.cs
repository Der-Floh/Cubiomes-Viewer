using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.PresetRepo;
using Cubiomes_Viewer.Engine.Service.AppDataServ;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

public sealed class SearchPresetService : ISearchPresetService
{
    public const int MaxNameLength = 80;

    public IReadOnlyList<SearchPreset> Examples => ExampleSearchPresets.All;
    public IReadOnlyList<SearchPreset> UserPresets => _userPresets;

    public event EventHandler? UserPresetsChanged;

    private readonly ISearchPresetRepository _repository;
    private readonly IUnreadableDataService _unreadable;
    private List<SearchPreset> _userPresets = [];

    public SearchPresetService(ISearchPresetRepository repository, IUnreadableDataService unreadable)
    {
        _repository = repository;
        _unreadable = unreadable;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _userPresets = [.. await ReadAsync(cancellationToken)];
        _userPresets.Sort(CompareNames);
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task SaveAsync(string name, SearchSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ArgumentException("Give the preset a name.", nameof(name));
        if (trimmed.Length > MaxNameLength)
            throw new ArgumentException($"A preset name may be at most {MaxNameLength} characters long.", nameof(name));

        return StoreAsync([.. _userPresets.Where(preset => !IsNamed(preset, trimmed)), new SearchPreset { Name = trimmed, Setup = setup }], cancellationToken);
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        => StoreAsync([.. _userPresets.Where(preset => !IsNamed(preset, name))], cancellationToken);

    private async Task<IReadOnlyList<SearchPreset>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.LoadAsync(cancellationToken);
        }
        catch (UnreadableDataException ex)
        {
            await _unreadable.KeepAsync(ex, cancellationToken);
            return [];
        }
    }

    private async Task StoreAsync(List<SearchPreset> presets, CancellationToken cancellationToken)
    {
        presets.Sort(CompareNames);
        await _repository.SaveAsync(presets, cancellationToken);
        _userPresets = presets;
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static int CompareNames(SearchPreset first, SearchPreset second) => StringComparer.CurrentCultureIgnoreCase.Compare(first.Name, second.Name);

    private static bool IsNamed(SearchPreset preset, string? name) => string.Equals(preset.Name, name?.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
