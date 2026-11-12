using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Opens the example presets and the user's own, and saves the search as it is set up as a preset.</summary>
public sealed partial class SearchPresetsViewModel : ViewModelBase
{
    public string ExamplesLabel { get; } = "Start from an example";
    public string UserPresetsLabel { get; } = "Your presets";
    public string NamePlaceholder { get; } = "Name for this search";
    public string SaveLabel { get; } = "Save as preset";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string NewName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasUserPresets { get; set; }

    public IReadOnlyList<PresetEntryViewModel> Examples { get; }
    public ObservableCollection<PresetEntryViewModel> UserPresets { get; } = [];

    /// <summary>Raised when a preset is to be opened.</summary>
    public event EventHandler<SearchPreset>? Opened;

    private readonly ISearchPresetService _presets;
    private readonly SearchSetupViewModel _setup;
    private readonly INotificationService _notifications;

    public SearchPresetsViewModel(ISearchPresetService presets, SearchSetupViewModel setup, INotificationService notifications) : base(notifications)
    {
        _presets = presets;
        _setup = setup;
        _notifications = notifications;

        Examples = [.. presets.Examples.Select(preset => new PresetEntryViewModel(preset, Open))];
        ShowUserPresets();
        presets.UserPresetsChanged += OnUserPresetsChanged;
    }

    public Task LoadAsync() => _presets.LoadAsync();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunGuardedAsync(async () =>
    {
        var name = NewName.Trim();
        await _presets.SaveAsync(name, _setup.Setup);
        NewName = string.Empty;
        _notifications.ShowSuccess("Preset saved", name);
    });

    private bool CanSave() => !string.IsNullOrWhiteSpace(NewName);

    private void Open(SearchPreset preset) => Opened?.Invoke(this, preset);

    private Task DeleteAsync(SearchPreset preset) => RunGuardedAsync(() => _presets.DeleteAsync(preset.Name));

    private void OnUserPresetsChanged(object? sender, EventArgs e) => ShowUserPresets();

    private void ShowUserPresets()
    {
        UserPresets.Clear();
        foreach (var preset in _presets.UserPresets)
            UserPresets.Add(new PresetEntryViewModel(preset, Open, DeleteAsync));
        HasUserPresets = UserPresets.Count > 0;
    }
}
