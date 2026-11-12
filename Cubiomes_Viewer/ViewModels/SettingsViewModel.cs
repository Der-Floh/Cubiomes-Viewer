using System.Collections.Frozen;
using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.ThemeServ;
using Cubiomes_Viewer.Service.WindowServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private const long BytesPerMegabyte = 1024 * 1024;
    private const double SmallMarkerScale = 0.75;
    private const double LargeMarkerScale = 1.35;
    private const int MaxGridSpacing = 1_000_000;

    public string Title { get; } = "Settings";
    public string AppearanceLabel { get; } = "Appearance";
    public string ThemeLabel { get; } = "Theme";
    public string MapLabel { get; } = "Map";
    public string SmoothMotionLabel { get; } = "Smooth motion";
    public string CacheLabel { get; } = "Tile cache (MB)";
    public string GridSpacingLabel { get; } = "Grid lines every, in blocks";
    public string GridMultiplierLabel { get; } = "Zoomed out, spaced wider by";
    public string MarkerSizeLabel { get; } = "Marker size";
    public string ZoomLimitsLabel { get; } = "When structures are marked";
    public string ZoomLimitsHint { get; } = "A structure type is marked while the map shows at most this many blocks per pixel. Higher limits show more at once but can crowd the map and take longer.";
    public string StartLabel { get; } = "Start";
    public string RestoreLabel { get; } = "Open the last world and search";
    public string RestoreHint { get; } = "When off, the app starts with a random seed and an empty search.";
    public string RememberWindowLabel { get; } = "Remember the window size and position";
    public string ExportLabel { get; } = "Export";
    public string CsvSeparatorLabel { get; } = "CSV separator";
    public string DiagnosticsLabel { get; } = "Diagnostics";
    public decimal MinCacheMegabytes { get; } = MapSettings.MinCacheMegabytes;
    public decimal MaxCacheMegabytes { get; } = MapSettings.MaxCacheMegabytes;
    public decimal MinGridSpacingValue { get; } = 1;
    public decimal MaxGridSpacingValue { get; } = MaxGridSpacing;

    [ObservableProperty]
    public partial ComboChoice<AppTheme>? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial bool SmoothMotion { get; set; } = true;

    [ObservableProperty]
    public partial decimal? CacheMegabytes { get; set; } = MapSettings.DefaultCacheMegabytes;

    [ObservableProperty]
    public partial decimal? GridSpacing { get; set; } = MapSettings.DefaultGridSpacing;

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedGridMultiplier { get; set; }

    [ObservableProperty]
    public partial ComboChoice<MarkerSize>? SelectedMarkerSize { get; set; }

    [ObservableProperty]
    public partial bool RestoreLastSession { get; set; } = true;

    [ObservableProperty]
    public partial bool RememberWindow { get; set; } = true;

    [ObservableProperty]
    public partial ComboChoice<CsvSeparator>? SelectedCsvSeparator { get; set; }

    /// <summary>Whether the app has a window whose size and position can be kept, as on the desktop.</summary>
    public bool CanRememberWindow { get; }

    public ObservableCollection<ComboChoice<AppTheme>> Themes { get; } =
    [
        new() { Display = "Follow system", Value = AppTheme.System },
        new() { Display = "Light", Value = AppTheme.Light },
        new() { Display = "Dark", Value = AppTheme.Dark },
    ];

    public ObservableCollection<ComboChoice<int>> GridMultipliers { get; } =
    [
        new() { Display = "2 (doubling)", Value = 2 },
        new() { Display = "4", Value = 4 },
        new() { Display = "5", Value = 5 },
        new() { Display = "10 (decimal)", Value = 10 },
    ];

    public ObservableCollection<ComboChoice<MarkerSize>> MarkerSizes { get; } =
    [
        new() { Display = "Small", Value = MarkerSize.Small },
        new() { Display = "Normal", Value = MarkerSize.Normal },
        new() { Display = "Large", Value = MarkerSize.Large },
    ];

    public ObservableCollection<ComboChoice<CsvSeparator>> CsvSeparators { get; } =
    [
        new() { Display = "Comma ( , )", Value = CsvSeparator.Comma },
        new() { Display = "Semicolon ( ; )", Value = CsvSeparator.Semicolon },
        new() { Display = "Tab", Value = CsvSeparator.Tab },
    ];

    /// <summary>Every structure type the map can mark, with how far out it is marked.</summary>
    public ObservableCollection<StructureZoomLimitViewModel> ZoomLimits { get; } = [];

    public WorldSettingsViewModel World { get; }
    public SelfTestViewModel SelfTest { get; }

    private readonly MapViewModel _map;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IMapTileService _tileService;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IStructureFinder _structureFinder;
    private readonly IVersionCatalog _versionCatalog;
    private bool _isShowingSettings;

    public SettingsViewModel(MapViewModel map, WorldSettingsViewModel world, SelfTestViewModel selfTest, ISettingsService settingsService, IThemeService themeService, IMapTileService tileService,
        IStructureCatalog structureCatalog, IStructureFinder structureFinder, IVersionCatalog versionCatalog, IWindowService windowService)
    {
        _map = map;
        _settingsService = settingsService;
        _themeService = themeService;
        _tileService = tileService;
        _structureCatalog = structureCatalog;
        _structureFinder = structureFinder;
        _versionCatalog = versionCatalog;
        World = world;
        SelfTest = selfTest;
        CanRememberWindow = windowService.HasWindows;
        SelectedTheme = Themes[0];
        SelectedGridMultiplier = GridMultipliers[0];
        SelectedMarkerSize = MarkerSizes[1];
        SelectedCsvSeparator = CsvSeparators[0];
        ShowZoomLimits([]);
    }

    /// <summary>Shows and applies the stored settings; called once they are loaded.</summary>
    public void ShowSettings()
    {
        World.ShowSettings();

        var settings = _settingsService.Current;
        _isShowingSettings = true;
        try
        {
            SelectedTheme = Themes.First(choice => choice.Value == settings.Appearance.Theme);
            SmoothMotion = settings.Map.SmoothMotion;
            CacheMegabytes = Math.Clamp(settings.Map.CacheMegabytes, MapSettings.MinCacheMegabytes, MapSettings.MaxCacheMegabytes);
            GridSpacing = Math.Clamp(settings.Map.GridSpacing, 1, MaxGridSpacing);
            SelectedGridMultiplier = GridMultipliers.FirstOrDefault(choice => choice.Value == settings.Map.GridMultiplier) ?? GridMultipliers[0];
            SelectedMarkerSize = MarkerSizes.FirstOrDefault(choice => choice.Value == settings.Map.MarkerSize) ?? MarkerSizes[1];
            RestoreLastSession = settings.Session.RestoreLastSession;
            RememberWindow = settings.Session.RememberWindow;
            SelectedCsvSeparator = CsvSeparators.FirstOrDefault(choice => choice.Value == settings.Export.CsvSeparator) ?? CsvSeparators[0];
            ShowZoomLimits(settings.Map.ZoomLimits);
        }
        finally
        {
            _isShowingSettings = false;
        }

        Apply();
    }

    partial void OnSelectedThemeChanged(ComboChoice<AppTheme>? value) => Store(settings => settings.Appearance.Theme = value?.Value ?? AppTheme.System);

    partial void OnSmoothMotionChanged(bool value) => Store(settings => settings.Map.SmoothMotion = value);

    partial void OnCacheMegabytesChanged(decimal? value)
    {
        if (value is { } megabytes)
            Store(settings => settings.Map.CacheMegabytes = (int)megabytes);
    }

    partial void OnGridSpacingChanged(decimal? value)
    {
        if (value is { } spacing)
            Store(settings => settings.Map.GridSpacing = (int)Math.Clamp(Math.Round(spacing), 1, MaxGridSpacing));
    }

    partial void OnSelectedGridMultiplierChanged(ComboChoice<int>? value) => Store(settings => settings.Map.GridMultiplier = value?.Value ?? MapSettings.DefaultGridMultiplier);

    partial void OnSelectedMarkerSizeChanged(ComboChoice<MarkerSize>? value) => Store(settings => settings.Map.MarkerSize = value?.Value ?? MarkerSize.Normal);

    partial void OnRestoreLastSessionChanged(bool value) => Store(settings => settings.Session.RestoreLastSession = value);

    partial void OnRememberWindowChanged(bool value) => Store(settings => settings.Session.RememberWindow = value);

    partial void OnSelectedCsvSeparatorChanged(ComboChoice<CsvSeparator>? value) => Store(settings => settings.Export.CsvSeparator = value?.Value ?? CsvSeparator.Comma);

    private void OnZoomLimitChanged(StructureZoomLimitViewModel limit)
        => Store(settings => settings.Map.ZoomLimits = [.. ZoomLimits.Where(row => row.IsChanged).Select(row => new StructureZoomLimit { Type = row.Type, MaxBlocksPerPixel = row.SelectedLimit?.Value ?? row.DefaultLimit })]);

    private void ShowZoomLimits(IReadOnlyList<StructureZoomLimit> stored)
    {
        ZoomLimits.Clear();
        var structures = _structureCatalog.Structures;
        foreach (var structure in structures)
        {
            var name = structures.Count(other => other.Name == structure.Name) > 1 ? $"{structure.Name} ({DescribeDimension(structure)})" : structure.Name;
            var limit = stored.LastOrDefault(entry => entry.Type == structure.Type)?.MaxBlocksPerPixel ?? structure.MaxBlocksPerPixel;
            ZoomLimits.Add(new StructureZoomLimitViewModel(structure.Type, name, structure.MaxBlocksPerPixel, limit, OnZoomLimitChanged));
        }
    }

    private string DescribeDimension(StructureInfo structure)
        => _structureFinder.TryGetPlacement(structure.Type, _versionCatalog.Newest, out var placement) ? placement.Dimension.ToString() : string.Empty;

    private void Store(Action<AppSettings> change)
    {
        if (_isShowingSettings)
            return;

        change(_settingsService.Current);
        _settingsService.RequestSave();
        Apply();
    }

    private void Apply()
    {
        var settings = _settingsService.Current;
        _themeService.Apply(settings.Appearance.Theme);
        _map.SmoothMotion = settings.Map.SmoothMotion;
        _map.GridSpacing = settings.Map.GridSpacing;
        _map.GridMultiplier = settings.Map.GridMultiplier;
        _map.MarkerScale = GetMarkerScale(settings.Map.MarkerSize);
        _map.MarkerZoomLimits = settings.Map.ZoomLimits.GroupBy(limit => limit.Type).ToFrozenDictionary(group => group.Key, group => group.Last().MaxBlocksPerPixel);
        _tileService.CacheBudgetBytes = settings.Map.CacheMegabytes * BytesPerMegabyte;
    }

    private static double GetMarkerScale(MarkerSize size) => size switch
    {
        MarkerSize.Small => SmallMarkerScale,
        MarkerSize.Normal => 1,
        MarkerSize.Large => LargeMarkerScale,
        _ => throw new NotSupportedException($"Unsupported marker size: {size}"),
    };
}
