using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

/// <summary>The search on the real workers: it has to find what testing every seed by hand finds.</summary>
public sealed class SeedSearchServiceTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const long FirstSeed = -150_000;
    private const long LastSeed = 149_999;

    private static readonly SearchDefinition _slimeChunks = SearchRuns.SlimeChunks;

    private readonly ServiceProvider _services = CreateServices();
    private readonly ISeedSearchService _search;
    private readonly ConditionTestBench _bench = new();

    public SeedSearchServiceTests()
    {
        _search = _services.GetRequiredService<ISeedSearchService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _bench.Dispose();
    }

    [Fact]
    public async Task StartAsync_WholeRange_FindsTheSeedsThatTestingEachSeedFinds()
    {
        var setup = new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = FirstSeed, LastSeed = LastSeed }, MaxResults = 10_000 };
        var expected = FindByHand(setup);

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, _search.Results);
        Assert.Equal(SearchState.Finished, _search.State);
        Assert.Equal(LastSeed - FirstSeed + 1, _search.Progress.Scanned);
    }

    [Fact]
    public async Task StartAsync_ConditionsThatNeedTheWorld_FindsTheSeedsThatTestingEachSeedFinds()
    {
        var definition = ConditionTestBench.Define(
            Version,
            new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(1024) },
            new StructureCondition { Id = 2, RelativeTo = 1, Type = StructureType.RuinedPortal, Area = SearchArea.Square(700) });
        var setup = new SearchSetup { Definition = definition, Source = new SeedRangeSource { FirstSeed = 7000, LastSeed = 7399 }, MaxResults = 1000 };
        var expected = FindByHand(setup);

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, _search.Results);
    }

    [Fact]
    public async Task StartAsync_EnoughSeedsFound_StopsRightBehindTheLastOfThem()
    {
        var setup = new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = FirstSeed, LastSeed = LastSeed }, MaxResults = 7 };
        var expected = FindByHand(setup);

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(7, expected.Count);
        Assert.Equal(expected, _search.Results);
        Assert.Equal(SearchState.Finished, _search.State);
        Assert.Equal(expected[^1] - FirstSeed + 1, _search.Progress.Scanned);
        Assert.Null(_search.Progress.NextSeed);
    }

    [Fact]
    public async Task ResumeAsync_AfterTheSearchWasStopped_FindsWhatAnUninterruptedSearchFinds()
    {
        var setup = new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = FirstSeed, LastSeed = LastSeed }, MaxResults = 10_000 };
        var expected = FindByHand(setup);

        await SearchRuns.StartAndStopAtTheFirstProgressAsync(_search, setup);
        var paused = _search.Progress;
        await _search.ResumeAsync(TestContext.Current.CancellationToken);

        Assert.InRange(paused.Scanned, 1, LastSeed - FirstSeed);
        Assert.Equal((long?)(FirstSeed + paused.Scanned), paused.NextSeed);
        Assert.Equal(SearchState.Finished, _search.State);
        Assert.Equal(expected, _search.Results);
    }

    [Fact]
    public async Task Restore_SnapshotOfAStoppedSearch_ContinuesInAnotherServiceAsIfNothingHappened()
    {
        var setup = new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = FirstSeed, LastSeed = LastSeed }, MaxResults = 10_000 };
        var expected = FindByHand(setup);
        await SearchRuns.StartAndStopAtTheFirstProgressAsync(_search, setup);
        var snapshot = Assert.IsType<SearchSnapshot>(_search.CreateSnapshot());

        using var services = CreateServices();
        var restarted = services.GetRequiredService<ISeedSearchService>();
        restarted.Restore(snapshot);
        var restored = (restarted.State, restarted.Progress.NextSeed, Results: restarted.Results.ToList());
        await restarted.ResumeAsync(TestContext.Current.CancellationToken);

        Assert.Equal((SearchState.Paused, snapshot.NextSeed), (restored.State, restored.NextSeed));
        Assert.Equal(snapshot.Results, restored.Results);
        Assert.Equal(expected, restarted.Results);
        Assert.Equal(SearchState.Finished, restarted.State);
    }

    [Fact]
    public async Task Restore_SnapshotOfAFinishedSearch_IsFinished()
    {
        var setup = new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = 0, LastSeed = 999 }, MaxResults = 3 };
        await _search.StartAsync(setup, TestContext.Current.CancellationToken);
        var snapshot = Assert.IsType<SearchSnapshot>(_search.CreateSnapshot());

        using var services = CreateServices();
        var restarted = services.GetRequiredService<ISeedSearchService>();
        restarted.Restore(snapshot);

        Assert.True(snapshot.IsFinished);
        Assert.Equal(SearchState.Finished, restarted.State);
        Assert.Equal(snapshot, restarted.CreateSnapshot());
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.ResumeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_DefinitionWithProblems_ThrowsAndStartsNothing()
    {
        var setup = new SearchSetup { Definition = ConditionTestBench.Define(Version) };

        var exception = await Assert.ThrowsAsync<InvalidSearchException>(() => _search.StartAsync(setup, TestContext.Current.CancellationToken));

        Assert.Single(exception.Problems);
        Assert.Equal(SearchState.Idle, _search.State);
        Assert.Null(_search.CreateSnapshot());
    }

    [Fact]
    public async Task StartAsync_WhileASearchIsRunning_Throws()
    {
        var setup = new SearchSetup { Definition = SearchRuns.Never };
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var running = _search.StartAsync(setup, stopping.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _search.StartAsync(setup, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(_search.Reset);
        await stopping.CancelAsync();
        await running;

        Assert.Equal(SearchState.Paused, _search.State);
    }

    [Fact]
    public async Task Reset_AfterASearch_ForgetsIt()
    {
        await _search.StartAsync(new SearchSetup { Definition = _slimeChunks, Source = new SeedRangeSource { FirstSeed = 0, LastSeed = 999 }}, TestContext.Current.CancellationToken);

        _search.Reset();

        Assert.Equal(SearchState.Idle, _search.State);
        Assert.Empty(_search.Results);
        Assert.Null(_search.Setup);
        Assert.Equal(default, _search.Progress);
    }

    private List<long> FindByHand(SearchSetup setup)
    {
        var plan = TestEngine.CreateSearchPlanCompiler().Compile(setup.Definition);
        var matcher = new SeedMatcher();
        List<long> found = [];
        var range = Assert.IsType<SeedRangeSource>(setup.Source);
        for (var seed = range.FirstSeed; seed <= range.LastSeed && found.Count < setup.MaxResults; seed++)
        {
            if (matcher.IsMatch(plan, _bench.Open(setup.Definition, seed)))
                found.Add(seed);
        }

        return found;
    }

    private static ServiceProvider CreateServices() => new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();
}
