using System.Text;

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

        _settings.ShowSettings();

        Assert.Equal(128, _settings.ZoomLimits.Single(limit => limit.Type == StructureType.Mansion).SelectedLimit?.Value);
        Assert.Equal(CsvSeparator.Tab, _settings.SelectedCsvSeparator?.Value);
        Assert.False(_settings.RestoreLastSession);
        Assert.Equal(128, _map.MarkerZoomLimits[StructureType.Mansion]);
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
        Assert.StartsWith("Seed;Biome;Samples;Share;", Encoding.UTF8.GetString(Assert.IsType<byte[]>(storage.SavedContent)), StringComparison.Ordinal);
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
