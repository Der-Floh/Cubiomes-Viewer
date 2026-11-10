using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.ComputeServ;

public sealed class WorkerPoolComputeServiceTests
{
    private const int TimeoutMilliseconds = 30_000;
    private const int TileSamples = 16;
    private const int TileScale = 256;
    private static readonly JobPriority _visible = new(JobPriorityClass.Visible);

    private readonly JobHandlerRegistry _handlers = new([JobHandlerRegistration.Create<BiomeTileJob, TileData>(new BiomeTileJobHandler())]);
    private readonly CubiomesWorldGeneratorFactory _generatorFactory = new();

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_WithWorkers_ReturnsTheTileTheHandlerProduces()
    {
        using var pool = CreatePool(new FakeWorkerFactory(2, _ => StartWorker()));
        var job = CreateTileJob(tileX: 3);
        using var context = new WorkerContext(_generatorFactory);
        var expected = new BiomeTileJobHandler().Handle(job, context, CancellationToken.None);

        var tile = await pool.RunAsync(job, _visible, TestContext.Current.CancellationToken);

        Assert.Equal(job.Request, tile.Request);
        Assert.Equal(expected.Biomes, tile.Biomes);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_ManyJobs_GivesEachWorkerOneJobAtATime()
    {
        var factory = new FakeWorkerFactory(3, _ => StartWorker());
        using var pool = CreatePool(factory);

        var tiles = await Task.WhenAll(Enumerable.Range(0, 60).Select(
            tileX => pool.RunAsync(CreateTileJob(tileX), _visible, TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(0, 60), tiles.Select(tile => tile.Request.Tile.X));
        Assert.Equal(60, factory.Workers.Sum(worker => worker.Tiles.Count));
        Assert.All(factory.Workers, worker => Assert.Equal(1, worker.MaxConcurrentJobs));
        Assert.Equal(3, pool.WorkerCount);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_QueuedJobs_RunByPriorityThenSubmissionOrder()
    {
        var workerStart = new TaskCompletionSource();
        var factory = new FakeWorkerFactory(1, async _ =>
        {
            await workerStart.Task;
            return new HostWorker(CreateHost());
        });
        using var pool = CreatePool(factory);

        Task<TileData>[] tasks =
        [
            pool.RunAsync(CreateTileJob(1), new JobPriority(JobPriorityClass.Background), TestContext.Current.CancellationToken),
            pool.RunAsync(CreateTileJob(2), new JobPriority(JobPriorityClass.Visible, 5), TestContext.Current.CancellationToken),
            pool.RunAsync(CreateTileJob(3), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
            pool.RunAsync(CreateTileJob(4), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken),
            pool.RunAsync(CreateTileJob(5), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
        ];
        workerStart.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal([4, 3, 5, 2, 1], factory.Workers.Single().Tiles.Select(tile => tile.X));
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_CancelledWhileQueued_NeverReachesAWorker()
    {
        var workerStart = new TaskCompletionSource();
        var factory = new FakeWorkerFactory(1, async _ =>
        {
            await workerStart.Task;
            return new HostWorker(CreateHost());
        });
        using var pool = CreatePool(factory);
        using var cancellation = new CancellationTokenSource();

        var cancelled = pool.RunAsync(CreateTileJob(1), _visible, cancellation.Token);
        var kept = pool.RunAsync(CreateTileJob(2), _visible, TestContext.Current.CancellationToken);
        cancellation.Cancel();
        workerStart.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        await kept;
        Assert.Equal([2], factory.Workers.Single().Tiles.Select(tile => tile.X));
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_NoWorkerCanBeStarted_RunsJobsInsideThisRuntime()
    {
        var factory = new FakeWorkerFactory(3, _ => throw new ComputeWorkerException("Workers are not supported here."));
        using var pool = CreatePool(factory);

        var tiles = await Task.WhenAll(Enumerable.Range(0, 5).Select(
            tileX => pool.RunAsync(CreateTileJob(tileX), _visible, TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(0, 5), tiles.Select(tile => tile.Request.Tile.X));
        Assert.Equal(3, factory.StartCount);
        Assert.Equal(1, pool.WorkerCount);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_WorkerFails_ReplacesTheWorkerAndRetriesTheJob()
    {
        var factory = new FakeWorkerFactory(1, index => StartWorker(crashes: index == 0));
        using var pool = CreatePool(factory);

        var tile = await pool.RunAsync(CreateTileJob(1), _visible, TestContext.Current.CancellationToken);

        Assert.Equal(1, tile.Request.Tile.X);
        Assert.Equal(2, factory.StartCount);
        Assert.True(factory.Workers[0].IsDisposed);
        Assert.False(factory.Workers[1].IsDisposed);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_JobTakesDownTwoWorkers_FailsTheJobAndKeepsServing()
    {
        var factory = new FakeWorkerFactory(1, index => StartWorker(crashes: index < 2));
        using var pool = CreatePool(factory);

        var exception = await Assert.ThrowsAsync<ComputeJobException>(
            () => pool.RunAsync(CreateTileJob(1), _visible, TestContext.Current.CancellationToken));
        var tile = await pool.RunAsync(CreateTileJob(2), _visible, TestContext.Current.CancellationToken);

        Assert.IsType<ComputeWorkerException>(exception.InnerException);
        Assert.Equal(2, tile.Request.Tile.X);
        Assert.Equal(3, factory.StartCount);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_WorkerFailsAndCannotBeReplaced_RetriesTheJobInsideThisRuntime()
    {
        var factory = new FakeWorkerFactory(1, index => index == 0
            ? StartWorker(crashes: true)
            : throw new ComputeWorkerException("Workers are not supported here."));
        using var pool = CreatePool(factory);

        var tile = await pool.RunAsync(CreateTileJob(1), _visible, TestContext.Current.CancellationToken);

        Assert.Equal(1, tile.Request.Tile.X);
        Assert.Equal(2, factory.StartCount);
        Assert.Equal(1, pool.WorkerCount);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task RunAsync_JobFailsInsideTheWorker_ReportsTheFailureAndKeepsTheWorker()
    {
        var factory = new FakeWorkerFactory(1, _ => StartWorker());
        using var pool = CreatePool(factory);
        var layeredWorld = new WorldKey(1, McVersion.V1_12_2, Dimension.Overworld, GeneratorFlags.None);
        var unsupported = new BiomeTileJob(new TileRequest(layeredWorld, LayerMode.Biomes, 64, new TileKey(1024, 0, 0)), TileSamples);

        var exception = await Assert.ThrowsAsync<CubiomesNativeException>(
            () => pool.RunAsync(unsupported, _visible, TestContext.Current.CancellationToken));
        var tile = await pool.RunAsync(CreateTileJob(2), _visible, TestContext.Current.CancellationToken);

        Assert.Equal(NativeStatus.Unsupported, exception.Status);
        Assert.Equal(2, tile.Request.Tile.X);
        Assert.Equal(1, factory.StartCount);
    }

    [Fact(Timeout = TimeoutMilliseconds)]
    public async Task Dispose_StopsTheWorkersAndRejectsNewJobs()
    {
        var factory = new FakeWorkerFactory(1, _ => StartWorker());
        var pool = CreatePool(factory);
        await pool.RunAsync(CreateTileJob(1), _visible, TestContext.Current.CancellationToken);

        pool.Dispose();

        Assert.True(factory.Workers.Single().IsDisposed);
        Assert.Throws<ObjectDisposedException>(
            () => { _ = pool.RunAsync(CreateTileJob(2), _visible, TestContext.Current.CancellationToken); });
    }

    private WorkerPoolComputeService CreatePool(FakeWorkerFactory factory)
        => new(factory, _handlers, _generatorFactory, NullLogger<WorkerPoolComputeService>.Instance);

    private ComputeWorkerHost CreateHost() => new(_handlers, _generatorFactory);

    private Task<HostWorker> StartWorker(bool crashes = false)
        => Task.FromResult(new HostWorker(CreateHost()) { Crashes = crashes });

    private static BiomeTileJob CreateTileJob(int tileX)
    {
        var world = new WorldKey(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        return new BiomeTileJob(new TileRequest(world, LayerMode.Biomes, 64, new TileKey(TileScale, tileX, 0)), TileSamples);
    }

    private sealed class FakeWorkerFactory(int workerCount, Func<int, Task<HostWorker>> startWorker) : IComputeWorkerFactory
    {
        private readonly List<HostWorker> _workers = [];
        private int _startCount;

        public int WorkerCount => workerCount;
        public int StartCount => _startCount;

        public IReadOnlyList<HostWorker> Workers
        {
            get
            {
                lock (_workers)
                    return [.. _workers];
            }
        }

        public async Task<IComputeWorker> StartWorkerAsync(CancellationToken cancellationToken = default)
        {
            var index = Interlocked.Increment(ref _startCount) - 1;
            await Task.Yield();

            var worker = await startWorker(index);
            lock (_workers)
                _workers.Add(worker);
            return worker;
        }
    }

    /// <summary>A worker backed by a host in this process that records what the pool asks of it.</summary>
    private sealed class HostWorker(ComputeWorkerHost host) : IComputeWorker
    {
        private int _concurrentJobs;

        public List<TileKey> Tiles { get; } = [];
        public bool Crashes { get; init; }
        public int MaxConcurrentJobs { get; private set; }
        public bool IsDisposed { get; private set; }

        public async Task<byte[]> ExecuteAsync(byte[] request)
        {
            MaxConcurrentJobs = Math.Max(MaxConcurrentJobs, Interlocked.Increment(ref _concurrentJobs));
            try
            {
                await Task.Yield();
                if (Crashes)
                    throw new ComputeWorkerException("The worker crashed.");

                Tiles.Add(((BiomeTileJob)ComputeEnvelope.DeserializeJob(request)).Request.Tile);
                return host.Execute(request);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentJobs);
            }
        }

        public void Dispose()
        {
            IsDisposed = true;
            host.Dispose();
        }
    }
}
