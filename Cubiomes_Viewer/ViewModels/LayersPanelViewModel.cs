using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class LayersPanelViewModel : ViewModelBase
{
    public string Title { get; } = "Layers";
    public string MapLayerLabel { get; } = "Map layer";
    public string AllOctavesLabel { get; } = "All octaves";
    public string OctavesHint { get; } = "With fewer octaves only the widest features of the noise remain.";
    public string PotentialHint { get; } = "Biomes in which this structure cannot generate are dimmed.";
    public string OverlaysLabel { get; } = "Overlays";
    public string ShowGridLabel { get; } = "Coordinate grid";
    public string ShowSlimeChunksLabel { get; } = "Slime chunks";
    public string LandmarksLabel { get; } = "Landmarks";
    public string ShowGatewayLinksLabel { get; } = "Gateway links";
    public string StructuresLabel { get; } = "Structures";
    public string ShowAllLabel { get; } = "All";
    public string HideAllLabel { get; } = "None";
    public string ZoomHint { get; } = "A structure type appears once the map is zoomed in far enough to tell its positions apart.";
    public string NoStructuresLabel { get; } = "This version has no structures in this dimension.";

    [ObservableProperty]
    public partial ComboChoice<LayerMode>? SelectedLayer { get; set; }

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedOctaves { get; set; }

    /// <summary>Whether the shown layer is made of noise octaves the user can limit.</summary>
    [ObservableProperty]
    public partial bool HasOctaves { get; set; }

    [ObservableProperty]
    public partial ComboChoice<HeightStyle>? SelectedHeightStyle { get; set; }

    [ObservableProperty]
    public partial bool IsHeightShown { get; set; }

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedPotentialStructure { get; set; }

    [ObservableProperty]
    public partial bool IsPotentialShown { get; set; }

    [ObservableProperty]
    public partial bool ShowGrid { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowSlimeChunks { get; set; }

    [ObservableProperty]
    public partial bool ShowGatewayLinks { get; set; } = true;

    [ObservableProperty]
    public partial bool IsOverworld { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEnd { get; set; }

    [ObservableProperty]
    public partial bool HasStructures { get; set; }

    public BiomeColorsViewModel BiomeColors { get; }
    public MarkerToggleViewModel Spawn { get; }
    public MarkerToggleViewModel Strongholds { get; }

    /// <summary>The layers the current version has in the current dimension.</summary>
    public ObservableCollection<ComboChoice<LayerMode>> Layers { get; } = [];

    public ObservableCollection<ComboChoice<int>> OctaveChoices { get; } = [];

    public ObservableCollection<ComboChoice<HeightStyle>> HeightStyles { get; } =
    [
        new() { Display = "Shaded relief", Value = HeightStyle.Shaded },
        new() { Display = "Height colours", Value = HeightStyle.Tinted },
        new() { Display = "Contour lines", Value = HeightStyle.Contours },
        new() { Display = "Grayscale", Value = HeightStyle.Grayscale },
    ];

    /// <summary>The structure types of the current world whose biomes the generator can tell.</summary>
    public ObservableCollection<ComboChoice<StructureType>> PotentialStructures { get; } = [];

    /// <summary>The structure types of the current version and dimension.</summary>
    public ObservableCollection<MarkerToggleViewModel> Structures { get; } = [];

    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly IStructureCatalog _structureCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IMapStyleService _styleService;
    private readonly ISettingsService _settingsService;
    private readonly HashSet<StructureType> _visibleStructures = [];
    private bool _isUpdating;

    public LayersPanelViewModel(MapViewModel map, BiomeColorsViewModel biomeColors, WorldSession session, IStructureCatalog structureCatalog, ILayerCatalog layerCatalog, IMapStyleService styleService, ISettingsService settingsService)
    {
        BiomeColors = biomeColors;
        _map = map;
        _session = session;
        _structureCatalog = structureCatalog;
        _layerCatalog = layerCatalog;
        _styleService = styleService;
        _settingsService = settingsService;

        Spawn = new MarkerToggleViewModel("World spawn", styleService.Spawn, isChecked: true, _ => ApplyOverlays());
        Strongholds = new MarkerToggleViewModel("Strongholds", styleService.Stronghold, isChecked: true, _ => ApplyOverlays());
        Update(() => SelectedHeightStyle = HeightStyles.First(choice => choice.Value == map.HeightStyle));
        ShowLayers();
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
            ShowGatewayLinks = settings.ShowGatewayLinks;
            SelectedHeightStyle = HeightStyles.FirstOrDefault(choice => choice.Value == settings.HeightStyle) ?? HeightStyles[0];
            SelectedPotentialStructure = PotentialStructures.FirstOrDefault(choice => choice.Value == settings.PotentialStructure) ?? SelectedPotentialStructure;
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

    partial void OnSelectedLayerChanged(ComboChoice<LayerMode>? value)
    {
        if (!_isUpdating && value is not null)
            _session.Layer = value.Value;
    }

    partial void OnSelectedOctavesChanged(ComboChoice<int>? value)
    {
        if (!_isUpdating && value is not null)
            _session.Octaves = value.Value;
    }

    partial void OnSelectedHeightStyleChanged(ComboChoice<HeightStyle>? value) => ApplyOverlays();

    partial void OnSelectedPotentialStructureChanged(ComboChoice<StructureType>? value) => ApplyOverlays();

    partial void OnShowGridChanged(bool value) => ApplyOverlays();

    partial void OnShowSlimeChunksChanged(bool value) => ApplyOverlays();

    partial void OnShowGatewayLinksChanged(bool value) => ApplyOverlays();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorldSession.Version) or nameof(WorldSession.Dimension))
            ShowAvailableStructures();
        if (e.PropertyName is nameof(WorldSession.Version) or nameof(WorldSession.Dimension) or nameof(WorldSession.Layer) or nameof(WorldSession.Octaves))
            ShowLayers();
    }

    /// <summary>
    /// Offers the layers and octave limits of the current world and shows the choice of the session. A choice the
    /// world does not have falls back to biomes and to all octaves, in the session too.
    /// </summary>
    private void ShowLayers()
    {
        var available = _layerCatalog.GetAvailable(_session.Version, _session.Dimension);
        var layer = available.FirstOrDefault(info => info.Mode == _session.Layer) ?? available[0];
        var octaves = _session.Octaves < layer.MaxOctaves ? _session.Octaves : 0;

        Update(() =>
        {
            Replace(Layers, [.. available.Select(info => new ComboChoice<LayerMode> { Display = info.Name, Value = info.Mode })]);
            Replace(OctaveChoices, [.. Enumerable.Range(0, Math.Max(layer.MaxOctaves, 1)).Select(CreateOctaveChoice)]);
            SelectedLayer = Layers.First(choice => choice.Value == layer.Mode);
            SelectedOctaves = OctaveChoices.First(choice => choice.Value == octaves);
            HasOctaves = layer.MaxOctaves > 0;
            IsHeightShown = layer.Mode == LayerMode.Height;
            ShowPotentialStructures(layer.Mode);
        });

        _session.Layer = layer.Mode;
        _session.Octaves = octaves;
        if (SelectedPotentialStructure is { } potential)
            _map.PotentialStructure = potential.Value;
    }

    private void ShowPotentialStructures(LayerMode layer)
    {
        var structures = _structureCatalog.GetWithBiomeRule(_session.Version, _session.Dimension);
        Replace(PotentialStructures, [.. structures.Select(structure => new ComboChoice<StructureType> { Display = structure.Name, Value = structure.Type })]);
        SelectedPotentialStructure = PotentialStructures.FirstOrDefault(choice => choice.Value == _map.PotentialStructure) ?? PotentialStructures.FirstOrDefault();
        IsPotentialShown = layer == LayerMode.StructurePotential;
    }

    private ComboChoice<int> CreateOctaveChoice(int octaves) => new()
    {
        Display = octaves switch
        {
            0 => AllOctavesLabel,
            1 => "1 octave",
            _ => string.Create(CultureInfo.InvariantCulture, $"{octaves} octaves"),
        },
        Value = octaves,
    };

    /// <summary>Replaces the choices only when they differ, so that an open list keeps its selection.</summary>
    private static void Replace<T>(ObservableCollection<ComboChoice<T>> target, IReadOnlyList<ComboChoice<T>> choices)
    {
        if (target.Select(choice => choice.Value).SequenceEqual(choices.Select(choice => choice.Value)))
            return;

        target.Clear();
        foreach (var choice in choices)
            target.Add(choice);
    }

    private void ShowAvailableStructures()
    {
        Structures.Clear();
        foreach (var structure in _structureCatalog.GetAvailable(_session.Version, _session.Dimension))
            Structures.Add(CreateStructureToggle(structure));

        HasStructures = Structures.Count > 0;
        IsOverworld = _session.Dimension == Dimension.Overworld;
        IsEnd = _session.Dimension == Dimension.End;
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

    /// <summary>Runs changes to several choices without applying each one on its own; may be nested.</summary>
    private void Update(Action changes)
    {
        var wasUpdating = _isUpdating;
        _isUpdating = true;
        try
        {
            changes();
        }
        finally
        {
            _isUpdating = wasUpdating;
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
        _map.HeightStyle = SelectedHeightStyle?.Value ?? _map.HeightStyle;
        _map.PotentialStructure = SelectedPotentialStructure?.Value ?? _map.PotentialStructure;
        _map.ShowGrid = ShowGrid;
        _map.ShowSlimeChunks = ShowSlimeChunks;
        _map.ShowGatewayLinks = ShowGatewayLinks;
        _map.ShowSpawn = Spawn.IsChecked;
        _map.ShowStrongholds = Strongholds.IsChecked;
        _map.VisibleStructures = _visibleStructures.ToFrozenSet();
    }

    private void StoreOverlays()
    {
        var settings = _settingsService.Current.Map;
        settings.HeightStyle = _map.HeightStyle;
        settings.PotentialStructure = _map.PotentialStructure;
        settings.ShowGrid = ShowGrid;
        settings.ShowSlimeChunks = ShowSlimeChunks;
        settings.ShowGatewayLinks = ShowGatewayLinks;
        settings.ShowSpawn = Spawn.IsChecked;
        settings.ShowStrongholds = Strongholds.IsChecked;
        settings.VisibleStructures = [.. _visibleStructures.Order()];
        _settingsService.RequestSave();
    }
}
