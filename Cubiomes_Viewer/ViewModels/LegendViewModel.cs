using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.LayerServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Explains the colours of the shown layer when it is a layer of numbers.</summary>
public sealed partial class LegendViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MinimumText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaximumText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ColorRamp? Ramp { get; set; }

    private readonly MapViewModel _map;
    private readonly ILayerCatalog _layerCatalog;

    public LegendViewModel(MapViewModel map, ILayerCatalog layerCatalog)
    {
        _map = map;
        _layerCatalog = layerCatalog;

        ShowLayer();
        map.PropertyChanged += OnMapChanged;
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.Layer) or nameof(MapViewModel.World) or nameof(MapViewModel.HeightStyle))
            ShowLayer();
    }

    private void ShowLayer()
    {
        var layer = _layerCatalog.GetInfo(_map.Layer, _map.World.Version, _map.World.Dimension);
        IsVisible = layer.Values is not null;
        if (layer.Values is not { } values)
            return;

        Title = layer.Name;
        var ramp = values.GetRamp(new TileStyle(_map.HeightStyle));
        Ramp = ramp;
        MinimumText = Format(values, ramp.Minimum);
        MaximumText = Format(values, ramp.Maximum);
    }

    private static string Format(LayerValues values, double value) => value.ToString(values.Format, CultureInfo.InvariantCulture);
}
