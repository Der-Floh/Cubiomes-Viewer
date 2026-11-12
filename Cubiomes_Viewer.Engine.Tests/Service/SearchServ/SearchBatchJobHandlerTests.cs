using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

public sealed class SearchBatchJobHandlerTests : IDisposable
{
    private const int Seeds = 60_000;

    private readonly WorkerContext _workerContext = new(new CubiomesWorldGeneratorFactory());
    private readonly SearchBatchJobHandler _handler = new(TestEngine.CreateSearchPlanCompiler(), new SeedMatcher());

    public void Dispose() => _workerContext.Dispose();

    [Fact]
    public void Handle_WithoutLimits_TestsEverySeed()
    {
        var result = Handle(new SearchBatchJob(SearchRuns.SlimeChunks, 0, Seeds));

        Assert.Equal(Seeds, result.Count);
        Assert.True(result.Matches.Length > 3);
    }

    [Fact]
    public void Handle_EnoughMatches_StopsAtTheSeedThatMakesThemEnough()
    {
        var whole = Handle(new SearchBatchJob(SearchRuns.SlimeChunks, 0, Seeds));

        var result = Handle(new SearchBatchJob(SearchRuns.SlimeChunks, 0, Seeds) { MaxMatches = 3 });

        Assert.Equal(whole.Matches[..3], result.Matches);
        Assert.Equal(whole.Matches[2] + 1, result.Count);
    }

    [Fact]
    public void Handle_OutOfTime_StopsAfterTheFirstSeed()
    {
        var result = Handle(new SearchBatchJob(SearchRuns.SlimeChunks, 0, Seeds) { MaxMilliseconds = 0 });

        Assert.Equal(1, result.Count);
    }

    private SearchBatchResult Handle(SearchBatchJob job) => _handler.Handle(job, _workerContext, TestContext.Current.CancellationToken);
}
