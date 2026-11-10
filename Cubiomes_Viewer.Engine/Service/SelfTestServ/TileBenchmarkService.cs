using System.Diagnostics;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

public sealed class TileBenchmarkService : ITileBenchmarkService
{
    private const long Seed = 262;
    private const int Scale = 4;
    private const int SampleHeight = 64;
    private const int TilesPerSide = 8;
    private static readonly JobPriority _priority = new(JobPriorityClass.Background);

    private readonly IComputeService _computeService;
    private readonly IVersionCatalog _versionCatalog;

    public TileBenchmarkService(IComputeService computeService, IVersionCatalog versionCatalog)
    {
        _computeService = computeService;
        _versionCatalog = versionCatalog;
    }

    public async Task<TileBenchmarkResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var jobs = CreateJobs();
        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(jobs.Select(job => _computeService.RunAsync(job, _priority, cancellationToken))).ConfigureAwait(false);
        stopwatch.Stop();

        return new TileBenchmarkResult
        {
            TileCount = jobs.Length,
            Scale = Scale,
            WorkerCount = _computeService.WorkerCount,
            Duration = stopwatch.Elapsed,
        };
    }

    private BiomeTileJob[] CreateJobs()
    {
        var world = new WorldKey(Seed, _versionCatalog.Newest, Dimension.Overworld, GeneratorFlags.None);
        var samples = TileMetrics.For(world).TileSamples;

        return
        [
            .. from tileZ in Enumerable.Range(0, TilesPerSide)
               from tileX in Enumerable.Range(0, TilesPerSide)
               select new BiomeTileJob(new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(Scale, tileX, tileZ)), samples),
        ];
    }
}
