using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.Service.UiZoomServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly SettingsViewModel _settings;
    private readonly MapViewModel _map;

    public SettingsViewModelTests()
    {
        _settings = _services.GetRequiredService<SettingsViewModel>();
        _map = _services.GetRequiredService<MapViewModel>();
    }

    private AppSettings Stored => _services.GetRequiredService<ISettingsService>().Current;

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task ViewModels_BuiltAfterTheSettingsAreLoaded_KeepTheStoredChoices()
    {
        using var services = TestServices.Create();
        var stored = new AppSettings
        {
            Appearance = { Theme = AppTheme.Dark, UiZoom = 150 },
            Map = { GridMultiplier = 10, MarkerSize = MarkerSize.Large, ColorMap = "Mine" },
            Export = { CsvSeparator = CsvSeparator.Semicolon },
        };
        await services.GetRequiredService<ISettingsRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);
        var settingsService = services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync(TestContext.Current.CancellationToken);

        _ = services.GetRequiredService<SettingsViewModel>();
        _ = services.GetRequiredService<BiomeColorsViewModel>();

        var current = settingsService.Current;
        Assert.Equal((AppTheme.Dark, 150), (current.Appearance.Theme, current.Appearance.UiZoom));
        Assert.Equal((10, MarkerSize.Large, "Mine"), (current.Map.GridMultiplier, current.Map.MarkerSize, current.Map.ColorMap));
        Assert.Equal(CsvSeparator.Semicolon, current.Export.CsvSeparator);
    }

    [AvaloniaFact]
    public void BiomeRelief_SwitchedOn_ReachesTheMapAndIsStored()
    {
        Assert.False(_map.BiomeRelief);

        _settings.BiomeRelief = true;

        Assert.True(_map.BiomeRelief);
        Assert.True(Stored.Map.BiomeRelief);
    }

    [AvaloniaFact]
    public void GridAndMarkers_Changed_ReachTheMapAndAreStored()
    {
        _settings.GridSpacing = 100;
        _settings.SelectedGridMultiplier = _settings.GridMultipliers.Single(choice => choice.Value == 10);
        _settings.SelectedMarkerSize = _settings.MarkerSizes.Single(choice => choice.Value == MarkerSize.Large);

        Assert.Equal((100, 10), (_map.GridSpacing, _map.GridMultiplier));
        Assert.True(_map.MarkerScale > 1);
        Assert.Equal((100, 10, MarkerSize.Large), (Stored.Map.GridSpacing, Stored.Map.GridMultiplier, Stored.Map.MarkerSize));
    }

    [AvaloniaFact]
    public void MarkerSize_Large_DrawsMarkersLargerThanNormalAndIsStored()
    {
        _settings.SelectedMarkerSize = _settings.MarkerSizes.Single(choice => choice.Value == MarkerSize.Normal);
        var normal = _map.MarkerScale;

        _settings.SelectedMarkerSize = _settings.MarkerSizes.Single(choice => choice.Value == MarkerSize.Large);

        Assert.True(_map.MarkerScale > normal);
        Assert.Equal(MarkerSize.Large, Stored.Map.MarkerSize);
        Assert.Equal(["Small", "Normal", "Large"], _settings.MarkerSizes.Select(choice => choice.Display));
    }

    [AvaloniaFact]
    public void UiZoom_Chosen_ScalesTheInterfaceAndIsStored()
    {
        _settings.SelectedUiZoom = _settings.UiZooms.Single(choice => choice.Value == 150);

        Assert.Equal(1.5, _services.GetRequiredService<IUiZoomService>().Scale);
        Assert.Equal(150, Stored.Appearance.UiZoom);
        Assert.Equal(["100 %", "125 %", "150 %", "200 %"], _settings.UiZooms.Select(choice => choice.Display));
    }

    [AvaloniaFact]
    public void ZoomLimit_Changed_ReachesTheMapAndOnlyChangedOnesAreStored()
    {
        var village = _settings.ZoomLimits.Single(limit => limit.Type == StructureType.Village);

        village.SelectedLimit = village.Choices.Single(choice => choice.Value == 64);

        Assert.Equal(64, _map.MarkerZoomLimits[StructureType.Village]);
        var stored = Assert.Single(Stored.Map.ZoomLimits);
        Assert.Equal((StructureType.Village, 64.0), (stored.Type, stored.MaxBlocksPerPixel));
        Assert.Contains(_settings.ZoomLimits, limit => limit.Name == "Ruined portal (Nether)");
    }

    [AvaloniaFact]
    public void StoredSettings_AreShownWhenLoaded()
    {
        Stored.Map.ZoomLimits = [new StructureZoomLimit { Type = StructureType.Mansion, MaxBlocksPerPixel = 128 }];
        Stored.Export.CsvSeparator = CsvSeparator.Tab;
        Stored.Session.RestoreLastSession = false;
        Stored.Map.BiomeRelief = true;

        _settings.ShowSettings();

        Assert.Equal(128, _settings.ZoomLimits.Single(limit => limit.Type == StructureType.Mansion).SelectedLimit?.Value);
        Assert.Equal(CsvSeparator.Tab, _settings.SelectedCsvSeparator?.Value);
        Assert.False(_settings.RestoreLastSession);
        Assert.Equal(128, _map.MarkerZoomLimits[StructureType.Mansion]);
        Assert.True(_settings.BiomeRelief);
        Assert.True(_map.BiomeRelief);
    }

    [AvaloniaFact]
    public async Task CsvSeparator_Chosen_IsWhatAnalysisFilesUse()
    {
        _settings.SelectedCsvSeparator = _settings.CsvSeparators.Single(choice => choice.Value == CsvSeparator.Semicolon);
        _services.GetRequiredService<WorldSession>().Version = McVersion.V1_16_5;
        var biomes = Assert.IsType<BiomeAnalysisViewModel>(_services.GetRequiredService<AnalysisPanelViewModel>().Tabs[0]);
        _services.GetRequiredService<AnalysisScopeViewModel>().Size = 256;
        await biomes.RunCommand.ExecuteAsync(null);

        await biomes.SaveCommand.ExecuteAsync(null);

        var storage = (MemoryStorageService)_services.GetRequiredService<IStorageService>();
        Assert.StartsWith("Seed;Biome;Samples;Share;", Assert.IsType<string>(storage.SavedText), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Start_WithoutRestoring_OpensARandomSeedAndNoSearch()
    {
        var stored = new AppSettings { Session = { RestoreLastSession = false }, View = { Seed = 262, Version = McVersion.V1_18_2, CenterX = 5000 } };
        await _services.GetRequiredService<ISettingsRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);
        var setup = new SearchSetup { Definition = new SearchDefinition { Conditions = [new SpawnCondition { Id = 1, Area = SearchArea.Square(100) }] } };
        await _services.GetRequiredService<ISearchSessionRepository>().SaveAsync(new SearchSession { Setup = setup }, TestContext.Current.CancellationToken);

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        var session = _services.GetRequiredService<WorldSession>();
        Assert.NotEqual(262, session.Seed);
        Assert.Equal(McVersion.V1_18_2, session.Version);
        Assert.Equal(MapCamera.Default, main.Map.Camera);
        Assert.Empty(_services.GetRequiredService<SearchSetupViewModel>().Conditions);
    }
}
