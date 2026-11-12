using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

/// <summary>Searches the tests run on the real workers, and ways to interrupt them.</summary>
internal static class SearchRuns
{
    /// <summary>Rare enough that a few hundred thousand seeds hold only a few matches, and quick to test without biomes.</summary>
    public static SearchDefinition SlimeChunks { get; } = ConditionTestBench.Define(McVersion.V1_16_5, new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 17 });

    /// <summary>Never met: every chunk of the area would have to be a slime chunk.</summary>
    public static SearchDefinition Never { get; } = ConditionTestBench.Define(McVersion.V1_16_5, new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 81 });

    /// <summary>Starts a search and stops it as soon as it reports that it scanned seeds.</summary>
    public static async Task StartAndStopAtTheFirstProgressAsync(ISeedSearchService search, SearchSetup setup)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        search.Changed += StopOnceSeedsWereScanned;
        try
        {
            await search.StartAsync(setup, stopping.Token);
        }
        finally
        {
            search.Changed -= StopOnceSeedsWereScanned;
        }

        Assert.Equal(SearchState.Paused, search.State);

        void StopOnceSeedsWereScanned(object? sender, EventArgs e)
        {
            if (search.Progress.Scanned > 0)
                stopping.Cancel();
        }
    }
}
