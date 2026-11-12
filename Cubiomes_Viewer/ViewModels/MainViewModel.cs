using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public const int NoPanel = -1;

    public string LayersLabel { get; } = "Layers";
    public string SearchLabel { get; } = "Search";
    public string SettingsLabel { get; } = "Settings";

    public WorldBarViewModel WorldBar { get; }
    public MapViewModel Map { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPanel), nameof(IsPanelOpen))]
    public partial int SelectedPanelIndex { get; set; } = NoPanel;

    public ViewModelBase? CurrentPanel => SelectedPanelIndex >= 0 && SelectedPanelIndex < _panels.Length ? _panels[SelectedPanelIndex] : null;
    public bool IsPanelOpen => CurrentPanel is not null;

    /// <summary>Completes once the stored settings, the last view and the last search are loaded and shown.</summary>
    public Task Initialization { get; }

    private readonly ViewModelBase[] _panels;
    private readonly LayersPanelViewModel _layersPanel;
    private readonly SearchPanelViewModel _searchPanel;
    private readonly SettingsViewModel _settingsPanel;
    private readonly WorldSession _session;
    private readonly ISettingsService _settingsService;
    private readonly ISeedHistoryService _seedHistory;
    private bool _isInitialized;

    public MainViewModel(WorldBarViewModel worldBar, MapViewModel map, LayersPanelViewModel layersPanel, SearchPanelViewModel searchPanel, SettingsViewModel settingsPanel, WorldSession session, ISettingsService settingsService, ISeedHistoryService seedHistory, INotificationService notificationService) : base(notificationService)
    {
        WorldBar = worldBar;
        Map = map;
        _layersPanel = layersPanel;
        _searchPanel = searchPanel;
        _settingsPanel = settingsPanel;
        _panels = [layersPanel, searchPanel, settingsPanel];
        _session = session;
        _settingsService = settingsService;
        _seedHistory = seedHistory;

        Initialization = RunGuardedAsync(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        await _settingsService.LoadAsync();
        ShowStoredView();
        _layersPanel.ShowSettings();
        _settingsPanel.ShowSettings();

        _session.PropertyChanged += OnSessionChanged;
        Map.PropertyChanged += OnMapChanged;
        _seedHistory.Changed += StoreView;
        _isInitialized = true;

        await _searchPanel.LoadAsync();
    }

    private void ShowStoredView()
    {
        var view = _settingsService.Current.View;
        _seedHistory.Load(view.RecentSeeds);

        _session.Version = view.Version;
        _session.Dimension = view.Dimension;
        _session.LargeBiomes = view.LargeBiomes;
        _session.SampleHeight = view.SampleHeight;
        _session.Layer = view.Layer;
        _session.Octaves = view.Octaves;
        _session.Seed = view.Seed;
        Map.Camera = new MapCamera(view.CenterX, view.CenterZ, view.BlocksPerPixel > 0 ? view.BlocksPerPixel : MapCamera.DefaultBlocksPerPixel);
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e) => StoreView();

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.Camera))
            StoreView();
    }

    private void StoreView()
    {
        if (!_isInitialized)
            return;

        var view = _settingsService.Current.View;
        view.Seed = _session.Seed;
        view.Version = _session.Version;
        view.Dimension = _session.Dimension;
        view.LargeBiomes = _session.LargeBiomes;
        view.SampleHeight = _session.SampleHeight;
        view.Layer = _session.Layer;
        view.Octaves = _session.Octaves;
        view.CenterX = Map.Camera.CenterX;
        view.CenterZ = Map.Camera.CenterZ;
        view.BlocksPerPixel = Map.Camera.BlocksPerPixel;
        view.RecentSeeds = [.. _seedHistory.Seeds];
        _settingsService.RequestSave();
    }
}
