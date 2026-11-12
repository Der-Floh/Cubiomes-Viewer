using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

/// <summary>Searches that take their seeds from a list or from the quad-hut bases, on the real workers.</summary>
public sealed class SeedSourceSearchTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly ISeedSearchService _search;
    private readonly ConditionTestBench _bench = new();

    public SeedSourceSearchTests()
    {
        _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(new FileSystemAppDataStore(_directory)).BuildServiceProvider();
        _search = _services.GetRequiredService<ISeedSearchService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        _bench.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task StartAsync_SeedList_FindsTheMatchingSeedsInTheOrderOfTheList()
    {
        long[] seeds = [.. Enumerable.Range(0, 40_000).Select(index => (long)index * 7919 - 100_000).Reverse()];
        var setup = new SearchSetup { Definition = SearchRuns.SlimeChunks, Source = new SeedListSource { Seeds = seeds }, MaxResults = 10_000 };
        var plan = TestEngine.CreateSearchPlanCompiler().Compile(setup.Definition);
        var matcher = new SeedMatcher();
        var expected = seeds.Where(seed => matcher.IsMatch(plan, _bench.Open(setup.Definition, seed))).ToList();

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, _search.Results);
        Assert.Equal(seeds.Length, _search.Progress.Scanned);
        Assert.Equal(SearchState.Finished, _search.State);
    }

    [Fact]
    public async Task StartAsync_QuadHutSeeds_FindsOnlySeedsWithFourHutsAroundTheOrigin()
    {
        var quad = new QuadHutCondition { Id = 1, Quality = QuadQuality.Ideal, Area = SearchArea.Square(1024) };
        var setup = new SearchSetup
        {
            Definition = ConditionTestBench.Define(Version, quad),
            Source = new QuadHutSeedSource { Quality = QuadQuality.Ideal },
            MaxResults = 5,
        };
        List<double> preparing = [];
        _search.Changed += (_, _) =>
        {
            if (_search.Progress.Preparing is { } share)
                preparing.Add(share);
        };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(5, _search.Results.Count);
        Assert.NotEmpty(preparing);
        Assert.All(_search.Results, seed => Assert.Equal(MatchStatus.Matched, _bench.Evaluate(quad, Version, seed, SearchPass.Full).Status));
        Assert.Equal(_search.Results.Count, _search.Results.Distinct().Count());
    }

    [Fact]
    public async Task StartAsync_QuadHutSourceForAVersionWithoutHuts_Throws()
    {
        var setup = new SearchSetup
        {
            Definition = ConditionTestBench.Define(McVersion.Beta1_7, new SlimeChunkCondition { Id = 1 }),
            Source = new QuadHutSeedSource(),
        };

        var exception = await Assert.ThrowsAsync<InvalidSearchException>(() => _search.StartAsync(setup, TestContext.Current.CancellationToken));

        Assert.Equal("Swamp huts form no quads in this version.", Assert.Single(exception.Problems).Message);
    }

    [Fact]
    public async Task StartAsync_EmptySeedList_Throws()
    {
        var setup = new SearchSetup { Definition = SearchRuns.SlimeChunks, Source = new SeedListSource() };

        var exception = await Assert.ThrowsAsync<InvalidSearchException>(() => _search.StartAsync(setup, TestContext.Current.CancellationToken));

        Assert.Equal("Add at least one seed to the list.", Assert.Single(exception.Problems).Message);
    }
}
