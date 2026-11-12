using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.ThemeServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    private const long BytesPerMegabyte = 1024 * 1024;

    public string Title { get; } = "Settings";
    public string AppearanceLabel { get; } = "Appearance";
    public string ThemeLabel { get; } = "Theme";
    public string MapLabel { get; } = "Map";
    public string SmoothMotionLabel { get; } = "Smooth motion";
    public string CacheLabel { get; } = "Tile cache (MB)";
    public string DiagnosticsLabel { get; } = "Diagnostics";
    public decimal MinCacheMegabytes { get; } = MapSettings.MinCacheMegabytes;
    public decimal MaxCacheMegabytes { get; } = MapSettings.MaxCacheMegabytes;

    [ObservableProperty]
    public partial ComboChoice<AppTheme>? SelectedTheme { get; set; }

    [ObservableProperty]
    public partial bool SmoothMotion { get; set; } = true;

    [ObservableProperty]
    public partial decimal? CacheMegabytes { get; set; } = MapSettings.DefaultCacheMegabytes;

    public ObservableCollection<ComboChoice<AppTheme>> Themes { get; } =
    [
        new() { Display = "Follow system", Value = AppTheme.System },
        new() { Display = "Light", Value = AppTheme.Light },
        new() { Display = "Dark", Value = AppTheme.Dark },
    ];

    public WorldSettingsViewModel World { get; }
    public SelfTestViewModel SelfTest { get; }

    private readonly MapViewModel _map;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IMapTileService _tileService;
    private bool _isShowingSettings;

    public SettingsViewModel(MapViewModel map, WorldSettingsViewModel world, SelfTestViewModel selfTest, ISettingsService settingsService, IThemeService themeService, IMapTileService tileService)
    {
        _map = map;
        _settingsService = settingsService;
        _themeService = themeService;
        _tileService = tileService;
        World = world;
        SelfTest = selfTest;
        SelectedTheme = Themes[0];
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
        _tileService.CacheBudgetBytes = settings.Map.CacheMegabytes * BytesPerMegabyte;
    }
}
