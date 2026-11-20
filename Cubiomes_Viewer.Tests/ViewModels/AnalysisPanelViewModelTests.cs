using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class AnalysisPanelViewModelTests : IDisposable
{
    private const long Seed = 42;
    private const int ViewPixels = 1024;

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
        ShowMap(blocksPerPixel: 1);
    }

    private BiomeAnalysisViewModel Biomes => Assert.IsType<BiomeAnalysisViewModel>(_panel.Tabs[0]);
    private StructureAnalysisViewModel Structures => Assert.IsType<StructureAnalysisViewModel>(_panel.Tabs[1]);
    private LocationAnalysisViewModel Locations => Assert.IsType<LocationAnalysisViewModel>(_panel.Tabs[2]);
    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
    private MemoryStorageService Storage => (MemoryStorageService)_services.GetRequiredService<IStorageService>();
    private RecordingProgressService Progress => (RecordingProgressService)_services.GetRequiredService<IProgressService>();

    public void Dispose() => _services.Dispose();

    /// <summary>Shows a square of the map around a point, so that the analyses look at as many blocks on each side as the pixels times their blocks.</summary>
    private void ShowMap(double blocksPerPixel, BlockPos centre = default)
    {
        _map.ViewSize = (ViewPixels, ViewPixels);
        _map.Camera = new MapCamera(centre.X, centre.Z, blocksPerPixel);
    }

    [AvaloniaFact]
    public async Task Biomes_TheVisibleMap_ListsEachBiomeMostCommonFirst()
    {
        Biomes.SelectedScale = Biomes.Scales.Single(choice => choice.Value == 64);
        var view = _panel.Scope.TakeView();
        var census = Assert.Single(await _services.GetRequiredService<IAnalysisService>().CountBiomesAsync([new BiomeCensusRequest(_session.Key, view.Area, view.Reference, 64, 64)]));

        await Biomes.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal(census.Tallies.Length, Biomes.Rows.Count);
        Assert.Equal(Biomes.Rows.Select(row => row.Title).Distinct().Count(), Biomes.Rows.Count);
        Assert.StartsWith($"Counted {census.Samples} samples in 1 seed", Biomes.StatusText, StringComparison.Ordinal);
        Assert.Contains("nearest at ", Biomes.Rows[0].Detail, StringComparison.Ordinal);
        Assert.True(Biomes.HasRows);
    }

    [AvaloniaFact]
    public async Task Biomes_WhileCounted_FillTheStatusStripAndEmptyItAfter()
    {
        Biomes.SelectedScale = Biomes.Scales.Single(choice => choice.Value == 64);

        await Biomes.RunCommand.ExecuteAsync(null);

        Assert.Contains(Progress.Shares, share => share > 0);
        Assert.Null(Progress.Share);
        Assert.Equal((1, 1), (Progress.Begun, Progress.Ended));
    }

    [AvaloniaFact]
    public async Task Biomes_AreaTooLargeToCount_SayWhyAndEmptyTheStatusStrip()
    {
        ShowMap(blocksPerPixel: 1024);
        Biomes.SelectedScale = Biomes.Scales.Single(choice => choice.Value == 64);

        await Biomes.RunCommand.ExecuteAsync(null);

        Assert.Contains("samples", Assert.Single(Notifications.Errors), StringComparison.Ordinal);
        Assert.Null(Progress.Share);
        Assert.Equal((1, 1), (Progress.Begun, Progress.Ended));
    }

    [AvaloniaFact]
    public async Task Biomes_LineOfAnotherSeedChosen_OpensTheSeedAtTheNearestSampleAndStaysChosen()
    {
        await Biomes.RunCommand.ExecuteAsync(null);
        _session.Seed = 7;
        var row = Biomes.Rows[^1];

        Biomes.SelectedRow = row;

        var position = Assert.NotNull(row.Position);
        Assert.Same(row, Biomes.SelectedRow);
        Assert.Equal(Seed, _session.Seed);
        Assert.Equal((position.X, position.Z), ((int)_map.Camera.CenterX, (int)_map.Camera.CenterZ));
        var pick = _map.Selection;
        Assert.NotNull(pick);
        Assert.True(pick.IsSpot);
        Assert.Equal((row.Title, position), (pick.Name, pick.Position));
    }

    [AvaloniaFact]
    public async Task Structures_LineChosen_PicksTheStructureWhileItsMarkersAreHidden()
    {
        _map.VisibleStructures = new HashSet<StructureType>();
        ShowMap(blocksPerPixel: 4);
        await Structures.RunCommand.ExecuteAsync(null);
        var row = Structures.Rows[0];

        Structures.SelectedRow = row;

        var pick = _map.Selection;
        Assert.NotNull(pick);
        Assert.Equal(StructureType.Village, pick.Structure);
        Assert.Equal((row.Title, row.Position), (pick.Name, (BlockPos?)pick.Position));
        Assert.Equal(_services.GetRequiredService<IMapStyleService>().GetStructureMarker(StructureType.Village), pick.Marker);
    }

    [AvaloniaFact]
    public async Task Locations_StrongholdAndSpawnChosen_ArePickedAsTheMapPicksThem()
    {
        Locations.ShowConditions = false;
        ShowMap(blocksPerPixel: 8);
        await Locations.RunCommand.ExecuteAsync(null);
        var styleService = _services.GetRequiredService<IMapStyleService>();
        var stronghold = Locations.Rows.First(row => row.Title.StartsWith("Stronghold ", StringComparison.Ordinal));
        var spawn = Locations.Rows.Single(row => row.Title == "World spawn");

        Locations.SelectedRow = stronghold;
        var strongholdPick = _map.Selection;
        Locations.SelectedRow = spawn;

        Assert.NotNull(strongholdPick);
        Assert.Equal((stronghold.Title, StructureType.Stronghold, stronghold.Position), (strongholdPick.Name, strongholdPick.Structure, (BlockPos?)strongholdPick.Position));
        Assert.Equal(MapSelection.ForSpawn(styleService.Spawn, Assert.NotNull(spawn.Position)), _map.Selection);
    }

    [AvaloniaFact]
    public async Task Locations_PickClearedOnTheMap_LetsGoOfTheLineSoThatChoosingItAgainPicksItAgain()
    {
        Locations.ShowConditions = false;
        ShowMap(blocksPerPixel: 8);
        await Locations.RunCommand.ExecuteAsync(null);
        var stronghold = Locations.Rows.First(row => row.Title.StartsWith("Stronghold ", StringComparison.Ordinal));
        Locations.SelectedRow = stronghold;

        _map.Selection = null;
        var released = Locations.SelectedRow;
        Locations.SelectedRow = stronghold;

        Assert.Null(released);
        Assert.Same(stronghold, Locations.SelectedRow);
        Assert.Equal(stronghold.Pick, _map.Selection);
    }

    [AvaloniaFact]
    public async Task Locations_LineOfAConditionThatMarksNoSingleThing_LeavesNothingPicked()
    {
        UseConditions(new SlimeChunkCondition { Id = 1, Count = 1 });
        await Locations.RunCommand.ExecuteAsync(null);
        Locations.SelectedRow = Locations.Rows.Single(row => row.Title == "World spawn");

        Locations.SelectedRow = Locations.Rows.First(row => row.Title.StartsWith("1 · ", StringComparison.Ordinal));

        Assert.Null(_map.Selection);
    }

    [AvaloniaFact]
    public async Task Biomes_SavedAsCsv_HaveOneLinePerBiomeOfTheSeed()
    {
        await Biomes.RunCommand.ExecuteAsync(null);

        await Biomes.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(Storage.SavedContent);
        var lines = Assert.IsType<string>(Storage.SavedText).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
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
        ShowMap(blocksPerPixel: 4);

        await Structures.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.NotEmpty(Structures.Rows);
        string[] chosen = ["Village", "Ruined portal"];
        Assert.All(Structures.Rows, row => Assert.Contains(row.Title, chosen));
        var distances = Structures.Rows.Select(row => Assert.NotNull(row.Position).DistanceTo(new BlockPos(0, 0))).ToList();
        Assert.Equal(distances.Order(), distances);
    }

    [AvaloniaFact]
    public void Structures_UntilChosenInTheTab_AreTheOnesOnTheMap()
    {
        _map.VisibleStructures = new HashSet<StructureType> { StructureType.Village, StructureType.Monument };

        Assert.Equal(["Ocean monument", "Village"], Structures.Types.Where(toggle => toggle.IsChecked).Select(toggle => toggle.Name).Order());

        Structures.Types.Single(toggle => toggle.Name == "Igloo").IsChecked = true;
        _map.VisibleStructures = new HashSet<StructureType> { StructureType.DesertPyramid };

        Assert.Equal(["Igloo", "Ocean monument", "Village"], Structures.Types.Where(toggle => toggle.IsChecked).Select(toggle => toggle.Name).Order());
    }

    [AvaloniaFact]
    public async Task Structures_OfTheDimensionTheMapShows_AreTheOnlyOnesOfferedAndListed()
    {
        _session.Dimension = Dimension.Nether;
        ShowMap(blocksPerPixel: 4);
        foreach (var toggle in Structures.Types)
            toggle.IsChecked = toggle.Name is "Nether fortress" or "Bastion remnant";

        await Structures.RunCommand.ExecuteAsync(null);

        Assert.DoesNotContain(Structures.Types, toggle => toggle.Name is "Village" or "End city");
        Assert.Empty(Notifications.Errors);
        Assert.Equal(["Bastion remnant", "Nether fortress"], Structures.Rows.Select(row => row.Title).Distinct().Order());
        Assert.All(Structures.Rows, row => Assert.Equal(Dimension.Nether, row.World.Dimension));
    }

    [AvaloniaFact]
    public async Task Locations_ConditionsInTheNether_OpenTheNether()
    {
        _services.GetRequiredService<ISearchSessionService>().Setup = new SearchSetup
        {
            Definition = new SearchDefinition { Version = McVersion.V1_16_5, Conditions = [new StructureCondition { Id = 1, Type = StructureType.Fortress, Area = SearchArea.Square(4096) }] },
        };

        await Locations.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal(Dimension.Overworld, Locations.Rows.Single(row => row.Title == "World spawn").World.Dimension);
        var fortresses = Locations.Rows.Where(row => row.Title == "1 · Nether fortress").ToList();
        Assert.NotEmpty(fortresses);
        Assert.All(fortresses, row => Assert.Equal(Dimension.Nether, row.World.Dimension));
    }

    [AvaloniaFact]
    public async Task Locations_SpawnAndTheSearchConditions_AreListed()
    {
        ShowMap(blocksPerPixel: 4);
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
    public async Task Locations_Strongholds_AreTheOnesTheMapShowsNearestToItsMiddleFirst()
    {
        Locations.ShowConditions = false;
        ShowMap(blocksPerPixel: 6, new BlockPos(1000, -500));

        await Locations.RunCommand.ExecuteAsync(null);

        var view = _panel.Scope.TakeView();
        var strongholds = (await FindLandmarksAsync()).Strongholds;
        var expected = strongholds.Where(view.Contains).OrderBy(position => position.SquaredDistanceTo(view.Reference)).Select(position => (BlockPos?)position).ToList();
        Assert.Empty(Notifications.Errors);
        Assert.InRange(expected.Count, 1, strongholds.Length - 1);
        Assert.Equal(expected, Locations.Rows.Where(row => row.Title.StartsWith("Stronghold ", StringComparison.Ordinal)).Select(row => row.Position));
    }

    [AvaloniaFact]
    public async Task Locations_WhileTheMapShowsTheNether_LeaveOutTheSpawnAndTheStrongholds()
    {
        Locations.ShowConditions = false;
        _session.Dimension = Dimension.Nether;

        await Locations.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Empty(Locations.Rows);
    }

    [AvaloniaFact]
    public async Task Locations_ConditionsAroundTheOrigin_LookInWhatTheMapShowsAndThoseLimitedByFindingsAsFarAsTheyNeed()
    {
        ShowMap(blocksPerPixel: 4);
        UseConditions(
            new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(16) },
            new StrongholdCondition { Id = 2, Limit = SearchLimit.Findings });

        await Locations.RunCommand.ExecuteAsync(null);

        var view = _panel.Scope.TakeView();
        var villages = Locations.Rows.Where(row => row.Title == "1 · Village").Select(row => Assert.NotNull(row.Position)).ToList();
        var nearestStronghold = (await FindLandmarksAsync()).Strongholds.MinBy(position => position.SquaredDistanceTo(new BlockPos(0, 0)));
        Assert.Empty(Notifications.Errors);
        Assert.True(villages.Count > 1, "The villages were looked for only around the world origin.");
        Assert.All(villages, village => Assert.True(view.Contains(village)));
        Assert.Equal(nearestStronghold, Assert.NotNull(Assert.Single(Locations.Rows, row => row.Title == "2 · Stronghold").Position));
    }

    [AvaloniaFact]
    public async Task Locations_ConditionRelativeToAnother_KeepsItsOwnAreaAroundIt()
    {
        ShowMap(blocksPerPixel: 4);
        UseConditions(
            new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(16) },
            new StrongholdCondition { Id = 2, RelativeTo = 1, Area = SearchArea.Square(16) });

        await Locations.RunCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Contains(Locations.Rows, row => row.Title.StartsWith("Stronghold ", StringComparison.Ordinal));
        Assert.DoesNotContain(Locations.Rows, row => row.Title == "2 · Stronghold");
    }

    [AvaloniaFact]
    public void Scope_View_IsWhatTheMapShowsAroundItsMiddle()
    {
        _map.ViewSize = (800, 600);
        _map.Camera = new MapCamera(100, -50, 4);

        var view = _panel.Scope.TakeView();

        Assert.Equal("The seed on the map", _panel.Scope.SeedChoices[0].Display);
        Assert.Equal(new BlockPos(100, -50), view.Reference);
        Assert.Equal(SearchArea.Rectangle(-1500, -1250, 1699, 1149), view.AroundOrigin);
        Assert.Equal(Dimension.Overworld, view.Dimension);
    }

    [AvaloniaFact]
    public async Task Scope_MapWithoutASizeYet_IsNothingToLookAt()
    {
        _map.ViewSize = (0, 0);

        await Biomes.RunCommand.ExecuteAsync(null);

        Assert.Equal(["The map shows nothing yet to look at."], Notifications.Errors);
    }

    [AvaloniaFact]
    public async Task Scope_FoundSeeds_AreOfferedOnceTheSearchFoundSomeAndAnalysedInItsVersion()
    {
        var search = _services.GetRequiredService<ISeedSearchService>();
        var setup = new SearchSetup { Definition = new SearchDefinition { Version = McVersion.V1_12_2, Conditions = [new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 14 }] }, MaxResults = 3 };
        var offeredBefore = _panel.Scope.SeedChoices.Count;

        await search.StartAsync(setup, TestContext.Current.CancellationToken);
        _panel.Scope.SelectedSeeds = _panel.Scope.SeedChoices.Single(choice => choice.Value == SeedScope.FoundSeeds);

        Assert.Equal(1, offeredBefore);
        Assert.Equal("The found seeds (3)", _panel.Scope.SelectedSeeds.Display);
        Assert.Equal(search.Results, _panel.Scope.GetWorlds(Dimension.Overworld).Select(world => world.Seed));
        Assert.All(_panel.Scope.GetWorlds(Dimension.Overworld), world => Assert.Equal(McVersion.V1_12_2, world.Version));
    }

    private void UseConditions(params SearchCondition[] conditions)
        => _services.GetRequiredService<ISearchSessionService>().Setup = new SearchSetup { Definition = new SearchDefinition { Version = McVersion.V1_16_5, Conditions = conditions } };

    private async Task<WorldLandmarks> FindLandmarksAsync()
        => (await _services.GetRequiredService<IAnalysisService>().FindLandmarksAsync([_session.Key with { Dimension = Dimension.Overworld }], cancellationToken: TestContext.Current.CancellationToken))[0];
}
