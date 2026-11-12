using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.PresetRepo;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class SearchPanelViewModelTests : IDisposable
{
    private const int ManySlimeChunks = 14;
    private const int EveryChunk = 81;

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly SearchPanelViewModel _panel;
    private readonly WorldSession _session;
    private readonly ISeedSearchService _search;

    public SearchPanelViewModelTests()
    {
        _panel = _services.GetRequiredService<SearchPanelViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _search = _services.GetRequiredService<ISeedSearchService>();
    }

    public void Dispose() => _services.Dispose();

    private SearchSetupViewModel Setup => _panel.Setup;
    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();

    [AvaloniaFact]
    public void AddCondition_Structure_AddsAnOpenVillageConditionThatCanBeSearched()
    {
        var village = Add<StructureCondition>();

        Assert.Equal("1 · Village", village.Header);
        Assert.True(village.IsExpanded);
        Assert.True(Setup.IsValid);
        Assert.True(_panel.Run.StartCommand.CanExecute(null));
        var condition = Assert.IsType<StructureCondition>(Assert.Single(Setup.Setup.Definition.Conditions));
        Assert.Equal(StructureType.Village, condition.Type);
        Assert.Equal(SearchArea.Square(1024), condition.Area);
    }

    [AvaloniaFact]
    public void ConditionEdited_ShowsInTheSetupAndInItsSummary()
    {
        var village = Add<StructureCondition>();
        var fields = Assert.IsType<StructureFieldsViewModel>(village.Fields);

        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.Outpost);
        village.SelectedShape = village.Shapes.Single(choice => choice.Value == AreaShape.Circle);
        village.Distance = 300;
        village.Count = 2;

        Assert.Equal("1 · Pillager outpost", village.Header);
        Assert.Equal("at least 2 · within a radius of 300 blocks around the world origin", village.Summary);
        Assert.Equal(new StructureCondition { Id = 1, Type = StructureType.Outpost, Area = SearchArea.Circle(300), Count = 2 }, Assert.Single(Setup.Setup.Definition.Conditions));
    }

    [AvaloniaFact]
    public void StructureTrait_OfferedForTheChosenTypeOnly_AndDroppedWithAnotherType()
    {
        var structure = Add<StructureCondition>();
        var fields = Assert.IsType<StructureFieldsViewModel>(structure.Fields);

        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.EndCity);
        var endCityTraits = fields.Traits.Select(choice => choice.Display).ToList();
        fields.SelectedTrait = fields.Traits.Single(choice => choice.Value == StructureTrait.EndShip);
        var withShip = (structure.Header, structure.Summary, Assert.IsType<StructureCondition>(Assert.Single(Setup.Setup.Definition.Conditions)).Trait);
        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.DesertPyramid);

        Assert.Equal(["Any", "With an end ship"], endCityTraits);
        Assert.Equal(("1 · End city", "with an end ship · within 512 blocks of the world origin", StructureTrait.EndShip), withShip);
        Assert.False(fields.HasTraits);
        Assert.Equal(StructureTrait.None, Assert.IsType<StructureCondition>(Assert.Single(Setup.Setup.Definition.Conditions)).Trait);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public void AddCondition_QuadHuts_OffersTheHutsAndHowCloseTheyHaveToBe()
    {
        var quad = Add<QuadHutCondition>();
        var fields = Assert.IsType<QuadHutFieldsViewModel>(quad.Fields);

        fields.SelectedQuality = fields.Qualities.Single(choice => choice.Value == QuadQuality.Classic);
        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.Igloo);

        Assert.Equal(["Swamp hut", "Desert pyramid", "Jungle temple", "Igloo"], fields.Types.Select(choice => choice.Display));
        Assert.Equal("1 · Quad igloos", quad.Header);
        Assert.Equal("classic or better · within 1,024 blocks of the world origin", quad.Summary);
        Assert.True(Setup.IsValid);
        var condition = Assert.IsType<QuadHutCondition>(Assert.Single(Setup.Setup.Definition.Conditions));
        Assert.Equal((StructureType.Igloo, QuadQuality.Classic), (condition.Type, condition.Quality));
    }

    [AvaloniaFact]
    public void ReferenceChoices_OfferOnlyConditionsThatDoNotLookAroundTheCondition()
    {
        var first = Add<StructureCondition>();
        var second = Add<StructureCondition>();
        var third = Add<StructureCondition>();

        LookAround(second, first);
        LookAround(third, second);

        Assert.Equal([SearchConditionViewModel.OriginReference], first.ReferenceChoices.Select(choice => choice.Value));
        Assert.Equal([SearchConditionViewModel.OriginReference, 1], second.ReferenceChoices.Select(choice => choice.Value));
        Assert.Equal([SearchConditionViewModel.OriginReference, 1, 2], third.ReferenceChoices.Select(choice => choice.Value));
        Assert.True(first.HasRelativeConditions);
        Assert.False(third.HasRelativeConditions);
        Assert.Equal([null, 1, 2], Setup.Setup.Definition.Conditions.Select(condition => condition.RelativeTo));
        Assert.EndsWith("of condition 2", third.Summary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void RemoveCondition_OthersLookedAroundIt_TheyLookAroundWhatItLookedAround()
    {
        var first = Add<StructureCondition>();
        var second = Add<StructureCondition>();
        var third = Add<StructureCondition>();
        LookAround(second, first);
        LookAround(third, second);

        second.RemoveCommand.Execute(null);

        Assert.Equal([1, 3], Setup.Conditions.Select(condition => condition.Id));
        Assert.Equal(1, third.RelativeTo);
        Assert.Equal(1, third.SelectedReference?.Value);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public void Problems_SeedsThatCannotBeRead_AreShownAndKeepTheSearchFromStarting()
    {
        Add<StructureCondition>();

        Setup.Seeds.FirstSeedText = "seed";
        var unreadable = Setup.Problems.ToList();
        Setup.Seeds.FirstSeedText = "10";
        Setup.Seeds.LastSeedText = "-5";

        Assert.Contains(unreadable, problem => problem.StartsWith("The first seed must be a whole number", StringComparison.Ordinal));
        Assert.Equal(["The last seed comes before the first one."], Setup.Problems);
        Assert.False(Setup.IsValid);
        Assert.False(_panel.Run.StartCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Problems_OfOneCondition_AreShownOnThatCondition()
    {
        var village = Add<StructureCondition>();
        var biome = Add<BiomeCondition>();

        Assert.Equal(["Choose at least one biome."], biome.Problems);
        Assert.True(biome.HasProblems);
        Assert.False(village.HasProblems);
        Assert.Empty(Setup.Problems);
        Assert.False(Setup.IsValid);

        var fields = Assert.IsType<BiomeFieldsViewModel>(biome.Fields);
        fields.Biomes.First().IsChecked = true;

        Assert.False(biome.HasProblems);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public void BiomeFields_DimensionChanged_ListTheBiomesOfThatDimensionAndDropTheOthers()
    {
        var biome = Add<BiomeCondition>();
        var fields = Assert.IsType<BiomeFieldsViewModel>(biome.Fields);
        fields.Biomes.Single(toggle => toggle.Name == "Plains").IsChecked = true;

        fields.SelectedDimension = fields.Dimensions.Single(choice => choice.Value == Dimension.Nether);

        Assert.Equal(["Basalt Deltas", "Crimson Forest", "Nether Wastes", "Soul Sand Valley", "Warped Forest"], fields.Biomes.Select(toggle => toggle.Name));
        Assert.True(Assert.IsType<BiomeCondition>(Assert.Single(Setup.Setup.Definition.Conditions)).Biomes.IsEmpty);

        fields.Filter = "forest";
        Assert.Equal(["Crimson Forest", "Warped Forest"], fields.Biomes.Select(toggle => toggle.Name));
    }

    [AvaloniaFact]
    public void SeedSource_QuadHuts_PutsTheQuadAroundTheOriginByDefault()
    {
        Add<QuadHutCondition>();

        Setup.Seeds.SelectedKind = Setup.Seeds.Kinds.Single(choice => choice.Value == SeedSourceKind.QuadHuts);
        Setup.Seeds.SelectedQuadQuality = Setup.Seeds.QuadQualities.Single(choice => choice.Value == QuadQuality.Ideal);

        Assert.True(Setup.Seeds.IsQuadSource);
        Assert.False(Setup.Seeds.IsRangeSource);
        Assert.Equal(new QuadHutSeedSource { Quality = QuadQuality.Ideal }, Setup.Setup.Source);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public void SeedSource_QuadHutsInAVersionWithoutHuts_IsAProblem()
    {
        Add<SlimeChunkCondition>();
        _session.Version = McVersion.Beta1_7;

        Setup.Seeds.SelectedKind = Setup.Seeds.Kinds.Single(choice => choice.Value == SeedSourceKind.QuadHuts);

        Assert.Equal(["Swamp huts form no quads in this version."], Setup.Problems);
        Assert.False(Setup.IsValid);
    }

    [AvaloniaFact]
    public void SeedSource_List_ReadsOneSeedPerLineAsNumberOrText()
    {
        Add<StructureCondition>();

        Setup.Seeds.SelectedKind = Setup.Seeds.Kinds.Single(choice => choice.Value == SeedSourceKind.List);
        Setup.Seeds.SeedListText = "5\r\nhello\n\n  -3  ";

        var list = Assert.IsType<SeedListSource>(Setup.Setup.Source);
        Assert.Equal([5L, 99162322, -3], list.Seeds);
        Assert.Equal("3 seeds.", Setup.Seeds.SeedListSummary);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public void SeedSource_EmptyList_IsAProblem()
    {
        Add<StructureCondition>();

        Setup.Seeds.SelectedKind = Setup.Seeds.Kinds.Single(choice => choice.Value == SeedSourceKind.List);

        Assert.Equal(["Add at least one seed to the list."], Setup.Problems);
    }

    [AvaloniaFact]
    public void Show_SetupWithAQuadSource_ShowsItsFields()
    {
        var setup = new SearchSetup
        {
            Definition = new SearchDefinition { Conditions = [new QuadHutCondition { Id = 1 }] },
            Source = new QuadHutSeedSource { Type = StructureType.Igloo, Quality = QuadQuality.Classic, RegionX = 7, RegionZ = -2 },
        };

        Setup.Show(setup);

        Assert.True(Setup.Seeds.IsQuadSource);
        Assert.Equal(StructureType.Igloo, Setup.Seeds.SelectedQuadType?.Value);
        Assert.Equal(QuadQuality.Classic, Setup.Seeds.SelectedQuadQuality?.Value);
        Assert.Equal((7m, -2m), (Setup.Seeds.QuadRegionX, Setup.Seeds.QuadRegionZ));
        Assert.Equal(setup.Source, Setup.Setup.Source);
    }

    [AvaloniaFact]
    public void Presets_ExampleOpened_ShowsItsConditionsAndSeeds()
    {
        var example = _panel.Presets.Examples.Single(entry => entry.Name == "Quad witch huts near spawn");

        example.OpenCommand.Execute(null);

        Assert.Equal("1 · Quad swamp huts", Assert.Single(Setup.Conditions).Header);
        Assert.True(Setup.Seeds.IsQuadSource);
        Assert.True(Setup.IsValid);
    }

    [AvaloniaFact]
    public async Task Presets_SavedThenOpenedAndDeleted_ComeAndGo()
    {
        Add<StructureCondition>();
        _panel.Presets.NewName = "  My villages ";

        await _panel.Presets.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single(_panel.Presets.UserPresets);
        Setup.Conditions[0].RemoveCommand.Execute(null);
        saved.OpenCommand.Execute(null);
        var reopened = Setup.Conditions.Count;
        var delete = saved.DeleteCommand;
        Assert.NotNull(delete);
        await delete.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal("My villages", saved.Name);
        Assert.Equal(string.Empty, _panel.Presets.NewName);
        Assert.Equal(1, reopened);
        Assert.Empty(_panel.Presets.UserPresets);
        Assert.False(_panel.Presets.HasUserPresets);
    }

    [AvaloniaFact]
    public void Presets_WithoutAName_CannotBeSaved()
        => Assert.False(_panel.Presets.SaveCommand.CanExecute(null));

    [AvaloniaFact]
    public async Task Initialization_StoredPresets_AreListed()
    {
        SearchPreset[] stored = [new() { Name = "Kept", Setup = new SearchSetup { Definition = new SearchDefinition() } }];
        await _services.GetRequiredService<ISearchPresetRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);

        await _services.GetRequiredService<MainViewModel>().Initialization;

        Assert.Equal(["Kept"], _panel.Presets.UserPresets.Select(entry => entry.Name));
    }

    [AvaloniaFact]
    public void VersionChanged_OnTheMap_IsTheVersionTheSearchLooksAt()
    {
        Add<StructureCondition>();

        _session.Version = McVersion.V1_16_5;

        Assert.Equal(McVersion.V1_16_5, Setup.Setup.Definition.Version);
        Assert.Equal("Searches 1.16 worlds, as the map shows them.", Setup.VersionText);
    }

    [AvaloniaFact]
    public async Task Start_ValidSetup_FindsSeedsListsThemAndSaysItIsFinished()
    {
        SetUpSlimeChunks(ManySlimeChunks, maxResults: 4);

        await _panel.Run.StartCommand.ExecuteAsync(null);

        Assert.Empty(Notifications.Errors);
        Assert.Equal(_search.Results, _panel.Results.Seeds.Select(seed => seed.Value));
        Assert.Equal(4, _panel.Results.Seeds.Count);
        Assert.Equal("Found seeds (4)", _panel.Results.Title);
        Assert.Equal("Finished after finding 4 seeds.", _panel.Run.StatusText);
        Assert.False(_panel.Run.IsRunning);
        Assert.Contains(Notifications.Notifications, notification => notification.Type == NotificationType.Success && notification.Title == "Search finished");
    }

    [AvaloniaFact]
    public async Task Stop_RunningSearch_StopsItAndContinueGoesOnFromThere()
    {
        SetUpSlimeChunks(EveryChunk, maxResults: 1, lastSeed: string.Empty);

        var running = _panel.Run.StartCommand.ExecuteAsync(null);
        await TestWait.UntilAsync(() => _search.Progress.Scanned > 0, "The search never scanned a seed.");
        Assert.True(_panel.Run.IsRunning);
        Assert.False(_panel.OpenSessionCommand.CanExecute(null));
        _panel.Run.StopCommand.Execute(null);
        await running;
        var stoppedAt = _search.Progress.NextSeed;

        Assert.Equal(SearchState.Paused, _search.State);
        Assert.True(_panel.Run.CanContinue);
        Assert.False(_panel.Run.IsStartShown);
        Assert.Equal($"Stopped before seed {stoppedAt}.", _panel.Run.StatusText);

        var continuing = _panel.Run.ContinueCommand.ExecuteAsync(null);
        await TestWait.UntilAsync(() => _search.Progress.NextSeed > stoppedAt, "The search did not go on.");
        _panel.Run.StopCommand.Execute(null);
        await continuing;

        Assert.Equal(SearchState.Paused, _search.State);
        Assert.Empty(Notifications.Errors);
    }

    [AvaloniaFact]
    public async Task SetupChanged_AfterTheSearchStopped_StartsANewSearchInsteadOfContinuing()
    {
        SetUpSlimeChunks(EveryChunk, maxResults: 1, lastSeed: string.Empty);
        var running = _panel.Run.StartCommand.ExecuteAsync(null);
        await TestWait.UntilAsync(() => _search.Progress.Scanned > 0, "The search never scanned a seed.");
        _panel.Run.StopCommand.Execute(null);
        await running;

        Setup.MaxResults = 2;

        Assert.False(_panel.Run.CanContinue);
        Assert.True(_panel.Run.IsStartShown);
        Assert.Equal("Stopped. The setup has changed since, so a new search starts over.", _panel.Run.StatusText);

        _panel.Run.ClearCommand.Execute(null);

        Assert.Equal(SearchState.Idle, _search.State);
        Assert.False(_panel.Run.HasSearch);
        Assert.Empty(_panel.Results.Seeds);
    }

    [AvaloniaFact]
    public async Task SelectedSeed_Chosen_OpensOnTheMapInTheVersionItWasFoundIn()
    {
        _session.Version = McVersion.V1_16_5;
        SetUpSlimeChunks(ManySlimeChunks, maxResults: 2);
        await _panel.Run.StartCommand.ExecuteAsync(null);
        _session.Version = McVersion.V1_21_3;
        var seed = _panel.Results.Seeds[1];

        _panel.Results.SelectedSeed = seed;

        Assert.Equal(seed.Value, _session.Seed);
        Assert.Equal(McVersion.V1_16_5, _session.Version);
        Assert.Equal(seed.Value, _services.GetRequiredService<Engine.Service.SeedServ.ISeedHistoryService>().Seeds[0]);
    }

    [AvaloniaFact]
    public async Task Copy_FoundSeeds_PutsOnePerLineOnTheClipboard()
    {
        SetUpSlimeChunks(ManySlimeChunks, maxResults: 3);
        await _panel.Run.StartCommand.ExecuteAsync(null);

        await _panel.Results.CopyCommand.ExecuteAsync(null);

        var clipboard = (RecordingClipboardService)_services.GetRequiredService<IClipboardService>();
        Assert.Equal(string.Join(Environment.NewLine, _search.Results), Assert.Single(clipboard.Texts));
    }

    [AvaloniaFact]
    public async Task OpenSession_SavedFile_BringsBackItsSetup()
    {
        var storage = (MemoryStorageService)_services.GetRequiredService<IStorageService>();
        Add<StructureCondition>().Count = 3;
        Setup.Seeds.FirstSeedText = "100";
        Setup.Seeds.LastSeedText = "5000";
        await _panel.SaveSessionCommand.ExecuteAsync(null);
        Setup.Conditions[0].RemoveCommand.Execute(null);
        Setup.Seeds.FirstSeedText = "0";

        storage.ContentToOpen = storage.SavedContent;
        await _panel.OpenSessionCommand.ExecuteAsync(null);

        Assert.Equal("search.json", storage.LastSaveOptions?.SuggestedFileName);
        Assert.Empty(Notifications.Errors);
        Assert.Equal(3, Assert.Single(Setup.Conditions).Count);
        Assert.Equal("100", Setup.Seeds.FirstSeedText);
        Assert.Equal("5000", Setup.Seeds.LastSeedText);
    }

    [AvaloniaFact]
    public async Task OpenSession_FileThatIsNoSession_SaysSoAndKeepsTheSetup()
    {
        var storage = (MemoryStorageService)_services.GetRequiredService<IStorageService>();
        Add<StructureCondition>();
        storage.ContentToOpen = "{ not json"u8.ToArray();

        await _panel.OpenSessionCommand.ExecuteAsync(null);

        Assert.Equal(["This is not a search session of Cubiomes Viewer."], Notifications.Errors);
        Assert.Single(Setup.Conditions);
    }

    [AvaloniaFact]
    public async Task Initialization_StoredSearch_IsShownStoppedWhereItWasAndCanContinue()
    {
        var setup = new SearchSetup
        {
            Definition = new SearchDefinition { Version = McVersion.V1_21_3, Conditions = [new StructureCondition { Id = 1, Type = StructureType.Village }] },
            Source = new SeedRangeSource { FirstSeed = 10, LastSeed = 99_999 },
        };
        var stored = new SearchSession { Setup = setup, Search = new SearchSnapshot { Setup = setup, Scanned = 490, NextSeed = 500, Results = [12, 400] } };
        await _services.GetRequiredService<ISearchSessionRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);

        await _services.GetRequiredService<MainViewModel>().Initialization;

        Assert.Empty(Notifications.Errors);
        Assert.Equal("1 · Village", Assert.Single(Setup.Conditions).Header);
        Assert.Equal("10", Setup.Seeds.FirstSeedText);
        Assert.Equal("99999", Setup.Seeds.LastSeedText);
        Assert.True(_panel.Run.CanContinue);
        Assert.Equal("Stopped before seed 500.", _panel.Run.StatusText);
        Assert.Equal([12L, 400L], _panel.Results.Seeds.Select(seed => seed.Value));
    }

    private SearchConditionViewModel Add<TCondition>() where TCondition : SearchCondition
    {
        Setup.ConditionKinds.Single(kind => kind.Descriptor.ConditionType == typeof(TCondition)).AddCommand.Execute(null);
        return Setup.Conditions[^1];
    }

    private static void LookAround(SearchConditionViewModel condition, SearchConditionViewModel reference)
        => condition.SelectedReference = condition.ReferenceChoices.Single(choice => choice.Value == reference.Id);

    private void SetUpSlimeChunks(int count, int maxResults, string lastSeed = "199999")
    {
        Add<SlimeChunkCondition>().Count = count;
        Setup.Seeds.FirstSeedText = "0";
        Setup.Seeds.LastSeedText = lastSeed;
        Setup.MaxResults = maxResults;
    }
}
