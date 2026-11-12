using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

public sealed class StructureSeedBatchJobHandlerTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const int SisterSeeds = 65_536;
    private const int Plains = 1;

    private static readonly SearchDefinition _desertTemple = ConditionTestBench.Define(Version, new StructureCondition { Id = 1, Type = StructureType.DesertPyramid, Area = SearchArea.Square(512) });

    private readonly WorkerContext _workerContext = new(new CubiomesWorldGeneratorFactory());
    private readonly ConditionTestBench _bench = new();
    private readonly SearchPlanCompiler _compiler = TestEngine.CreateSearchPlanCompiler();
    private readonly SeedMatcher _matcher = new();
    private readonly StructureSeedBatchJobHandler _handler;
    private readonly SearchBatchJobHandler _seedBySeed;

    public StructureSeedBatchJobHandlerTests()
    {
        _handler = new StructureSeedBatchJobHandler(_compiler, _matcher);
        _seedBySeed = new SearchBatchJobHandler(_compiler, _matcher);
    }

    public void Dispose()
    {
        _workerContext.Dispose();
        _bench.Dispose();
    }

    [Fact]
    public void Handle_SisterSeedsOfTwoStructureSeeds_FindsWhatTestingEachSeedFinds()
    {
        var random = new Random(48);
        var (ruledOut, withMatches) = (0, 0);

        for (var batch = 0; batch < 30; batch++)
        {
            var job = new StructureSeedBatchJob(_desertTemple, random.NextInt64(StructureSeed.Max), SisterSeeds - 60, 120);
            long[] seeds = [.. Enumerable.Range(0, job.Count).Select(job.GetSeed)];

            var result = Handle(job);
            var expected = _seedBySeed.Handle(new SearchBatchJob(_desertTemple, seeds[0], seeds.Length, Seeds: seeds), _workerContext, TestContext.Current.CancellationToken);

            Assert.Equal(job.Count, result.Count);
            Assert.Equal(expected.Matches, result.Matches);
            ruledOut += QuickOutcome(_desertTemple, job.FirstStructureSeed) == SearchOutcome.Failed ? 1 : 0;
            withMatches += result.Matches.Length > 0 ? 1 : 0;
        }

        Assert.InRange(ruledOut, 1, 29);
        Assert.InRange(withMatches, 1, 29);
    }

    [Fact]
    public void Handle_StructureSeedsTheQuickPassRulesOut_SkipsTheirSisterSeeds()
    {
        var quad = ConditionTestBench.Define(Version, new QuadHutCondition { Id = 1, Quality = QuadQuality.Ideal, Area = SearchArea.Square(1024) });
        var job = new StructureSeedBatchJob(quad, 0, 0, 20_000 * SisterSeeds);

        var result = Handle(job);

        Assert.Equal(job.Count, result.Count);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Handle_StructureSeedsTheQuickPassDecides_TakesAllTheirSisterSeedsUpToTheMostABatchReturns()
    {
        var slimeChunks = ConditionTestBench.Define(Version, new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 12 });
        long[] passing = [.. Enumerable.Range(0, 1000).Select(seed => (long)seed).Where(seed => QuickOutcome(slimeChunks, seed) == SearchOutcome.Passed).Take(2)];
        var (first, second) = (passing[0], passing[1]);
        var job = new StructureSeedBatchJob(slimeChunks, first, 0, (int)(second - first + 1) * SisterSeeds);
        var fromSecond = (int)(second - first) * SisterSeeds;

        var result = Handle(job);

        Assert.Equal(StructureSeedBatchJobHandler.MaxMatches, result.Matches.Length);
        Assert.Equal(Enumerable.Range(0, SisterSeeds).Concat(Enumerable.Range(fromSecond, StructureSeedBatchJobHandler.MaxMatches - SisterSeeds)), result.Matches);
        Assert.Equal(result.Matches[^1] + 1, result.Count);
    }

    [Fact]
    public void Handle_EnoughMatches_StopsAtTheSeedThatMakesThemEnough()
    {
        var slimeChunks = ConditionTestBench.Define(Version, new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 12 });
        var passing = Enumerable.Range(0, 1000).Select(seed => (long)seed).First(seed => QuickOutcome(slimeChunks, seed) == SearchOutcome.Passed);
        var job = new StructureSeedBatchJob(slimeChunks, passing, 0, 3 * SisterSeeds) { MaxMatches = 5 };

        var result = Handle(job);

        Assert.Equal([0, 1, 2, 3, 4], result.Matches);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Handle_OutOfTime_StopsAfterTheFirstSeedItHadToTestByItself()
    {
        var plains = ConditionTestBench.Define(Version, new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(Plains), Area = SearchArea.Circle(64), Scale = 16 });
        var job = new StructureSeedBatchJob(plains, 77, 10, 1000) { MaxMilliseconds = 0 };

        int[] expected = _matcher.IsMatch(_compiler.Compile(plains), _bench.Open(plains, job.GetSeed(0))) ? [0] : [];

        var result = Handle(job);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Matches);
    }

    [Fact]
    public void Handle_OutOfTimeAfterAStructureSeedItRuledOut_StopsAtTheEndOfItsSisterSeeds()
    {
        var quad = ConditionTestBench.Define(Version, new QuadHutCondition { Id = 1, Quality = QuadQuality.Ideal, Area = SearchArea.Square(1024) });
        var job = new StructureSeedBatchJob(quad, 1234, 100, 3 * SisterSeeds) { MaxMilliseconds = 0 };

        var result = Handle(job);

        Assert.Equal(SearchOutcome.Failed, QuickOutcome(quad, 1234));
        Assert.Equal(SisterSeeds - 100, result.Count);
        Assert.Empty(result.Matches);
    }

    private SearchBatchResult Handle(StructureSeedBatchJob job) => _handler.Handle(job, _workerContext, TestContext.Current.CancellationToken);

    private SearchOutcome QuickOutcome(SearchDefinition definition, long seed) => _matcher.Evaluate(_compiler.Compile(definition), _bench.Open(definition, seed), SearchPass.Quick);
}
