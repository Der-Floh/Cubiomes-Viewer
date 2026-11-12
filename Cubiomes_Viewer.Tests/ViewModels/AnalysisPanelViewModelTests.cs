using System.Text;

using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class AnalysisPanelViewModelTests : IDisposable
{
    private const long Seed = 42;

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly AnalysisPanelViewModel _panel;
    private readonly WorldSession _session;
    private readonly MapViewModel _map;

    public AnalysisPanelViewModelTests()
    {
        _panel = _services.GetRequiredService<AnalysisPanelViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _map = _services.GetRequiredService<MapViewModel>();
        _session.Version = McVersion.V1_16_5;
        _session.Seed = Seed;
        _panel.Scope.Size = 1024;
    }

    private BiomeAnalysisViewModel Biomes => Assert.IsType<BiomeAnalysisViewModel>(_panel.Tabs[0]);
    private StructureAnalysisViewModel Structures => Assert.IsType<StructureAnalysisViewModel>(_panel.Tabs[1]);
    private LocationAnalysisViewModel Locations => Assert.IsType<LocationAnalysisViewModel>(_panel.Tabs[2]);
    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
    private MemoryStorageService Storage => (MemoryStorageService)_services.GetRequiredService<IStorageService>();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task Biomes_TheSeedOnTheMap_ListsEachBiomeMostCommonFirst()
    {
        Biomes.SelectedScale = Biomes.Scales.Single(choice => choice.Value == 64);
        var census = Assert.Single(await _services.GetRequiredService<IAnalysisService>().CountBiomesAsync([new BiomeCensusRequest(_session.Key, SearchArea.Square(1024), new BlockPos(0, 0), 64, 64)]));

        await Biomes.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal(census.Tallies.Length, Biomes.Rows.Count);
        Assert.Equal(Biomes.Rows.Select(row => row.Title).Distinct().Count(), Biomes.Rows.Count);
        Assert.StartsWith($"Counted {census.Samples} samples in 1 seed", Biomes.StatusText, StringComparison.Ordinal);
        Assert.Contains("nearest at ", Biomes.Rows[0].Detail, StringComparison.Ordinal);
        Assert.True(Biomes.HasRows);
    }

    [AvaloniaFact]
    public async Task Biomes_LineChosen_OpensTheSeedAtTheNearestSample()
    {
        await Biomes.RunCommand.ExecuteAsync(null);
        _session.Seed = 7;
        var row = Biomes.Rows[^1];

        Biomes.SelectedRow = row;

        var position = Assert.NotNull(row.Position);
        Assert.Equal(Seed, _session.Seed);
        Assert.Equal((position.X, position.Z), ((int)_map.Camera.CenterX, (int)_map.Camera.CenterZ));
    }

    [AvaloniaFact]
    public async Task Biomes_SavedAsCsv_HaveOneLinePerBiomeOfTheSeed()
    {
        await Biomes.RunCommand.ExecuteAsync(null);

        await Biomes.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(Storage.SavedContent);
        var lines = Encoding.UTF8.GetString(Storage.SavedContent).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("biomes.csv", Storage.LastSaveOptions?.SuggestedFileName);
        Assert.Equal("Seed,Biome,Samples,Share,Nearest X,Nearest Z,Distance", lines[0]);
        Assert.Equal(Biomes.Rows.Count + 1, lines.Length);
        Assert.All(lines.Skip(1), line => Assert.StartsWith("42,", line, StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Structures_ChosenTypes_AreListedNearestFirst()
    {
        foreach (var toggle in Structures.Types)
            toggle.IsChecked = toggle.Name is "Village" or "Ruined portal";
        _panel.Scope.Size = 4096;

        await Structures.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.NotEmpty(Structures.Rows);
        string[] chosen = ["Village", "Ruined portal"];
        Assert.All(Structures.Rows, row => Assert.Contains(row.Title, chosen));
        var distances = Structures.Rows.Select(row => Assert.NotNull(row.Position).DistanceTo(new BlockPos(0, 0))).ToList();
        Assert.Equal(distances.Order(), distances);
    }

    [AvaloniaFact]
    public async Task Locations_SpawnAndTheSearchConditions_AreListed()
    {
        _services.GetRequiredService<ISearchSessionService>().Setup = new SearchSetup
        {
            Definition = new SearchDefinition { Version = McVersion.V1_16_5, Conditions = [new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(4096) }] },
        };

        await Locations.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal("World spawn", Locations.Rows[0].Title);
        Assert.Contains(Locations.Rows, row => row.Title == "1 · Village");
        Assert.EndsWith(" the conditions of the search.", Locations.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Scope_FoundSeeds_AreOfferedOnceTheSearchFoundSomeAndAnalysedInItsVersion()
    {
        var search = _services.GetRequiredService<ISeedSearchService>();
        var setup = new SearchSetup { Definition = new SearchDefinition { Version = McVersion.V1_12_2, Conditions = [new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 14 }] }, MaxResults = 3 };
        var offeredBefore = _panel.Scope.SeedChoices.Count;

        await search.StartAsync(setup, TestContext.Current.CancellationToken);
        _panel.Scope.SelectedSeeds = _panel.Scope.SeedChoices.Single(choice => choice.Value == AnalysisSeeds.FoundSeeds);

        Assert.Equal(1, offeredBefore);
        Assert.Equal("The found seeds (3)", _panel.Scope.SelectedSeeds.Display);
        Assert.Equal(search.Results, _panel.Scope.GetWorlds(Dimension.Overworld).Select(world => world.Seed));
        Assert.All(_panel.Scope.GetWorlds(Dimension.Overworld), world => Assert.Equal(McVersion.V1_12_2, world.Version));
    }

    [AvaloniaFact]
    public void Scope_MapCentre_BecomesThePointTheAreaLiesAround()
    {
        _map.GoTo(1234.6, -987.2);

        _panel.Scope.UseMapCenterCommand.Execute(null);

        Assert.Equal(new BlockPos(1235, -987), _panel.Scope.Reference);
    }
}
