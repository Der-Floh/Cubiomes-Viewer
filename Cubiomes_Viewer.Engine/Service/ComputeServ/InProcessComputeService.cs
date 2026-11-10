using System.Runtime.Versioning;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>Runs jobs on dedicated worker threads, each with its own native generators.</summary>
[UnsupportedOSPlatform("browser")]
public sealed class InProcessComputeService : IComputeService, IDisposable
{
    private const int MaxWorkers = 16;
    private const int WorkerStackBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(2);

    public int WorkerCount => _workers.Length;

    private readonly JobHandlerRegistry _handlers;
    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly ILogger<InProcessComputeService> _logger;
    private readonly ComputeQueue<IComputeWorkItem> _queue = new();
    private readonly Thread[] _workers;
    private bool _disposed;

    public InProcessComputeService(JobHandlerRegistry handlers, IWorldGeneratorFactory generatorFactory, ILogger<InProcessComputeService> logger)
    {
        _handlers = handlers;
        _generatorFactory = generatorFactory;
        _logger = logger;

        var workerCount = Math.Clamp(Environment.ProcessorCount - 1, 1, MaxWorkers);
        _workers = [.. Enumerable.Range(0, workerCount).Select(CreateWorker)];
        foreach (var worker in _workers)
            worker.Start();

        _logger.LogDebug("Started {WorkerCount} compute worker(s)", workerCount);
    }

    public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var item = new ComputeWorkItem<TResult>(job, _handlers.Resolve(job), cancellationToken);
        _queue.Enqueue(item, priority);
        return item.Task;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queue.Complete();
        foreach (var worker in _workers)
            worker.Join(_shutdownTimeout);
    }

    private Thread CreateWorker(int index)
        => new(RunWorker, WorkerStackBytes) { IsBackground = true, Name = $"Compute worker {index}" };

    private void RunWorker()
    {
        using var context = new WorkerContext(_generatorFactory);
        while (_queue.WaitDequeue(out var item))
            item.Execute(context);
    }
}
