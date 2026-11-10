using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SelfTestServ;

public sealed class TileBenchmarkServiceTests
{
    [Fact]
    public async Task RunAsync_WorkingGenerator_MeasuresABlockOfTiles()
    {
        var handlers = new JobHandlerRegistry([JobHandlerRegistration.Create<BiomeTileJob, TileData>(new BiomeTileJobHandler())]);
        using var computeService = new InlineComputeService(handlers, new CubiomesWorldGeneratorFactory());
        var benchmark = new TileBenchmarkService(computeService, new VersionCatalog());

        var result = await benchmark.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(64, result.TileCount);
        Assert.Equal(4, result.Scale);
        Assert.Equal(1, result.WorkerCount);
        Assert.True(result.Duration > TimeSpan.Zero);
        Assert.True(result.TilesPerSecond > 0);
    }

    [Fact]
    public void ToString_Result_StatesThroughputAndWorkers()
    {
        var single = new TileBenchmarkResult { TileCount = 64, Scale = 4, WorkerCount = 1, Duration = TimeSpan.FromSeconds(2) };
        var pooled = new TileBenchmarkResult { TileCount = 64, Scale = 4, WorkerCount = 6, Duration = TimeSpan.FromMilliseconds(500) };

        Assert.Equal("64 tiles at 1:4 in 2000 ms (32 tiles/s on 1 worker)", single.ToString());
        Assert.Equal("64 tiles at 1:4 in 500 ms (128 tiles/s on 6 workers)", pooled.ToString());
    }
}
