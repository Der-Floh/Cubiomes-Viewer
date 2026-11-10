using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class LayersPanelViewModel : ViewModelBase
{
    public string Title { get; } = "Layers";
    public string OverlaysLabel { get; } = "Overlays";
    public string ShowGridLabel { get; } = "Coordinate grid";
    public string ShowSlimeChunksLabel { get; } = "Slime chunks";
    public string LandmarksLabel { get; } = "Landmarks";
    public string StructuresLabel { get; } = "Structures";
    public string ShowAllLabel { get; } = "All";
    public string HideAllLabel { get; } = "None";
    public string ZoomHint { get; } = "A structure type appears once the map is zoomed in far enough to tell its positions apart.";
    public string NoStructuresLabel { get; } = "This version has no structures in this dimension.";

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSlimeChunks { get; set; }

    [ObservableProperty]
    public partial bool IsOverworld { get; set; } = true;

    [ObservableProperty]
    public partial bool HasStructures { get; set; }

    public MarkerToggleViewModel Spawn { get; }
    public MarkerToggleViewModel Strongholds { get; }

    /// <summary>The structure types of the current version and dimension.</summary>
    public ObservableCollection<MarkerToggleViewModel> Structures { get; } = [];

    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IMapStyleService _styleService;
    private readonly ISettingsService _settingsService;
    private readonly HashSet<StructureType> _visibleStructures = [];
    private bool _isUpdating;

    public LayersPanelViewModel(MapViewModel map, WorldSession session, IStructureCatalog structureCatalog, IMapStyleService styleService, ISettingsService settingsService)
    {
        _map = map;
        _session = session;
        _structureCatalog = structureCatalog;
        _styleService = styleService;
        _settingsService = settingsService;

        Spawn = new MarkerToggleViewModel("World spawn", styleService.Spawn, isChecked: true, _ => ApplyOverlays());
        Strongholds = new MarkerToggleViewModel("Strongholds", styleService.Stronghold, isChecked: true, _ => ApplyOverlays());
        session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>Shows the stored settings; called once they are loaded.</summary>
    public void ShowSettings()
    {
        var settings = _settingsService.Current.Map;
        Update(() =>
        {
            ShowGrid = settings.ShowGrid;
            ShowSlimeChunks = settings.ShowSlimeChunks;
            Spawn.IsChecked = settings.ShowSpawn;
            Strongholds.IsChecked = settings.ShowStrongholds;

            _visibleStructures.Clear();
            _visibleStructures.UnionWith(settings.VisibleStructures);
            ShowAvailableStructures();
        });
        ShowOverlaysOnMap();
    }

    [RelayCommand]
    public void ShowAllStructures() => SetAllStructures(isVisible: true);

    [RelayCommand]
    public void HideAllStructures() => SetAllStructures(isVisible: false);

    partial void OnShowGridChanged(bool value) => ApplyOverlays();

    partial void OnShowSlimeChunksChanged(bool value) => ApplyOverlays();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorldSession.Version) or nameof(WorldSession.Dimension))
            ShowAvailableStructures();
    }

    private void ShowAvailableStructures()
    {
        Structures.Clear();
        foreach (var structure in _structureCatalog.GetAvailable(_session.Version, _session.Dimension))
            Structures.Add(CreateStructureToggle(structure));

        HasStructures = Structures.Count > 0;
        IsOverworld = _session.Dimension == Dimension.Overworld;
    }

    private MarkerToggleViewModel CreateStructureToggle(StructureInfo structure)
        => new(structure.Name, _styleService.GetStructureMarker(structure.Type), _visibleStructures.Contains(structure.Type),
            isVisible => SetStructureVisible(structure.Type, isVisible));

    private void SetStructureVisible(StructureType type, bool isVisible)
    {
        if (isVisible)
            _visibleStructures.Add(type);
        else
            _visibleStructures.Remove(type);

        ApplyOverlays();
    }

    private void SetAllStructures(bool isVisible)
    {
        Update(() =>
        {
            foreach (var structure in Structures)
                structure.IsChecked = isVisible;
        });
        ApplyOverlays();
    }

    /// <summary>Runs changes to several toggles without applying each one on its own.</summary>
    private void Update(Action changes)
    {
        _isUpdating = true;
        try
        {
            changes();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void ApplyOverlays()
    {
        if (_isUpdating)
            return;

        ShowOverlaysOnMap();
        StoreOverlays();
    }

    private void ShowOverlaysOnMap()
    {
        _map.ShowGrid = ShowGrid;
        _map.ShowSlimeChunks = ShowSlimeChunks;
        _map.ShowSpawn = Spawn.IsChecked;
        _map.ShowStrongholds = Strongholds.IsChecked;
        _map.VisibleStructures = _visibleStructures.ToFrozenSet();
    }

    private void StoreOverlays()
    {
        var settings = _settingsService.Current.Map;
        settings.ShowGrid = ShowGrid;
        settings.ShowSlimeChunks = ShowSlimeChunks;
        settings.ShowSpawn = Spawn.IsChecked;
        settings.ShowStrongholds = Strongholds.IsChecked;
        settings.VisibleStructures = [.. _visibleStructures.Order()];
        _settingsService.RequestSave();
    }
}
