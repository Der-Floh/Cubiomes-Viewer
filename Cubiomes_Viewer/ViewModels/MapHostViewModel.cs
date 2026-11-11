using CommunityToolkit.Mvvm.Input;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Everything the map area shows and offers: the map itself, its context menu, the legend of its layer and the details of the picked marker.</summary>
public sealed partial class MapHostViewModel : ViewModelBase
{
    public MapViewModel Map { get; }
    public MapMenuViewModel Menu { get; }
    public SelectionViewModel Selection { get; }
    public LegendViewModel Legend { get; }

    public MapHostViewModel(MapViewModel map, MapMenuViewModel menu, SelectionViewModel selection, LegendViewModel legend)
    {
        Map = map;
        Menu = menu;
        Selection = selection;
        Legend = legend;
    }

    /// <summary>Ends the measurement if one is running, otherwise closes the details of the picked marker.</summary>
    [RelayCommand]
    public void Dismiss()
    {
        if (Map.IsMeasuring)
            Menu.StopMeasuring();
        else
            Selection.Close();
    }
}
