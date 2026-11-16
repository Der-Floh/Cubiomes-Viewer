using CommunityToolkit.Mvvm.Input;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Everything the map area shows and offers: the map itself, its context menu, the legend of its layer, the details of the picked marker
/// and the search for the nearest place.
/// </summary>
public sealed partial class MapHostViewModel : ViewModelBase
{
    public MapViewModel Map { get; }
    public MapMenuViewModel Menu { get; }
    public SelectionViewModel Selection { get; }
    public LegendViewModel Legend { get; }
    public NearestSearchViewModel Nearest { get; }

    public MapHostViewModel(MapViewModel map, MapMenuViewModel menu, SelectionViewModel selection, LegendViewModel legend, NearestSearchViewModel nearest)
    {
        Map = map;
        Menu = menu;
        Selection = selection;
        Legend = legend;
        Nearest = nearest;
    }

    /// <summary>Stops a search for the nearest place, the measurement or the placing of a pin if one is under way, otherwise closes the details of the picked marker.</summary>
    [RelayCommand]
    public void Dismiss() => TryDismiss();

    /// <summary>Does what <see cref="Dismiss"/> does; false when there was nothing to stop or close.</summary>
    public bool TryDismiss()
    {
        if (Nearest.IsRunning)
            Nearest.Stop();
        else if (Map.IsMeasuring)
            Menu.StopMeasuring();
        else if (Map.IsPlacingPin)
            Map.IsPlacingPin = false;
        else if (Selection.IsOpen)
            Selection.Close();
        else
            return false;

        return true;
    }
}
