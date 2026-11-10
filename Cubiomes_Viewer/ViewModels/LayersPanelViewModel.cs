using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Service.SettingsServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class LayersPanelViewModel : ViewModelBase
{
    public string Title { get; } = "Layers";
    public string OverlaysLabel { get; } = "Overlays";
    public string ShowGridLabel { get; } = "Coordinate grid";

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    private readonly MapViewModel _map;
    private readonly ISettingsService _settingsService;

    public LayersPanelViewModel(MapViewModel map, ISettingsService settingsService)
    {
        _map = map;
        _settingsService = settingsService;
    }

    /// <summary>Shows the stored settings; called once they are loaded.</summary>
    public void ShowSettings() => ShowGrid = _settingsService.Current.Map.ShowGrid;

    partial void OnShowGridChanged(bool value)
    {
        _map.ShowGrid = value;
        if (_settingsService.Current.Map.ShowGrid == value)
            return;

        _settingsService.Current.Map.ShowGrid = value;
        _settingsService.RequestSave();
    }
}
