using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Switches one kind of map marker on or off and reports each change to its owner.</summary>
public sealed partial class MarkerToggleViewModel : ObservableObject
{
    public string Name { get; }
    public MapMarker Marker { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    // Unset while the constructor assigns the initial state, which is not a change to report.
    private readonly Action<bool>? _changed;

    public MarkerToggleViewModel(string name, MapMarker marker, bool isChecked, Action<bool> changed)
    {
        Name = name;
        Marker = marker;
        IsChecked = isChecked;
        _changed = changed;
    }

    partial void OnIsCheckedChanged(bool value) => _changed?.Invoke(value);
}
