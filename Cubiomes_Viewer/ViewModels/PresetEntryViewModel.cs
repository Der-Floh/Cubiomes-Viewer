using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>A preset in a list or a menu, with the commands that open it and, for the user's own, delete it.</summary>
public sealed class PresetEntryViewModel
{
    public string Name => Preset.Name;
    public string Description => Preset.Description;
    public string DeleteLabel => $"Delete {Name}";
    public SearchPreset Preset { get; }
    public IRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand? DeleteCommand { get; }

    public PresetEntryViewModel(SearchPreset preset, Action<SearchPreset> open, Func<SearchPreset, Task>? delete = null)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(open);

        Preset = preset;
        OpenCommand = new RelayCommand(() => open(preset));
        DeleteCommand = delete is null ? null : new AsyncRelayCommand(() => delete(preset));
    }
}
