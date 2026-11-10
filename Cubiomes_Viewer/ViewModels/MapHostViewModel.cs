namespace Cubiomes_Viewer.ViewModels;

/// <summary>Everything the map area shows and offers: the map itself and its context menu.</summary>
public sealed class MapHostViewModel : ViewModelBase
{
    public MapViewModel Map { get; }
    public MapMenuViewModel Menu { get; }

    public MapHostViewModel(MapViewModel map, MapMenuViewModel menu)
    {
        Map = map;
        Menu = menu;
    }
}
