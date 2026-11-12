using CommunityToolkit.Mvvm.ComponentModel;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Chooses one biome for a biome condition and reports each change to its owner.</summary>
public sealed partial class BiomeToggleViewModel : ObservableObject
{
    public int Biome { get; }
    public string Name { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    // Unset while the constructor assigns the initial state, which is not a change to report.
    private readonly Action<BiomeToggleViewModel>? _changed;

    public BiomeToggleViewModel(int biome, string name, bool isChecked, Action<BiomeToggleViewModel> changed)
    {
        Biome = biome;
        Name = name;
        IsChecked = isChecked;
        _changed = changed;
    }

    partial void OnIsCheckedChanged(bool value) => _changed?.Invoke(this);
}
