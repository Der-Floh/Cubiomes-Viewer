using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// Runs jobs on a pool of workers that live in other runtimes, exchanging serialized jobs and results with them.
/// A worker that fails is replaced and its job goes to another worker once; when no worker can be started,
/// a worker inside this runtime takes over.
/// </summary>
public sealed class WorkerPoolComputeService : IComputeService, IDisposable
{
    private const int MaxAttemptsPerJob = 2;

    public int WorkerCount
    {
        get
        {
            lock (_gate)
                return Math.Max(_workers.Count + _pendingStarts, 1);
        }
    }

    private readonly IComputeWorkerFactory _workerFactory;
    private readonly JobHandlerRegistry _handlers;
    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly ILogger<WorkerPoolComputeService> _logger;
    private readonly ComputeQueue<IPoolWorkItem> _queue = new();
    private readonly HashSet<IComputeWorker> _workers = [];
    private readonly Stack<IComputeWorker> _idleWorkers = new();
    private readonly Lock _gate = new();
    private int _pendingStarts;
    private bool _isStarted;
    private bool _isDisposed;

    public WorkerPoolComputeService(IComputeWorkerFactory workerFactory, JobHandlerRegistry handlers, IWorldGeneratorFactory generatorFactory, ILogger<WorkerPoolComputeService> logger)
    {
        _workerFactory = workerFactory;
        _handlers = handlers;
        _generatorFactory = generatorFactory;
        _logger = logger;
        _pendingStarts = Math.Max(workerFactory.WorkerCount, 1);
    }

    public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var item = new PoolWorkItem<TResult>(ComputeEnvelope.SerializeJob(job), priority, cancellationToken);
        _queue.Enqueue(item, priority);
        StartWorkersOnce();
        Dispatch();
        return item.Task;
    }

    public void Dispose()
    {
        IComputeWorker[] workers;
        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            workers = [.. _workers];
            _workers.Clear();
            _idleWorkers.Clear();
        }

        foreach (var worker in workers)
            worker.Dispose();
    }

    private void StartWorkersOnce()
    {
        int workerCount;
        lock (_gate)
        {
            if (_isStarted)
                return;

            _isStarted = true;
            workerCount = _pendingStarts;
        }

        for (var index = 0; index < workerCount; index++)
            _ = StartWorkerAsync();
    }

    private async Task StartWorkerAsync()
    {
        var worker = await TryStartWorkerAsync().ConfigureAwait(false);
        if (TryAdopt(worker))
            Dispatch();
        else
            worker?.Dispose();
    }

    private async Task<IComputeWorker?> TryStartWorkerAsync()
    {
        try
        {
            return await _workerFactory.StartWorkerAsync().ConfigureAwait(false);
        }
        catch (ComputeWorkerException ex)
        {
            _logger.LogWarning(ex, "A compute worker could not be started");
            return null;
        }
    }

    /// <summary>Adds the started worker to the pool, or a local worker when it is missing and no other worker is left.</summary>
    private bool TryAdopt(IComputeWorker? worker)
    {
        lock (_gate)
        {
            _pendingStarts--;
            if (_isDisposed)
                return false;

            worker ??= _workers.Count == 0 && _pendingStarts == 0 ? CreateLocalWorker() : null;
            if (worker is null)
                return false;

            _workers.Add(worker);
            _idleWorkers.Push(worker);
            return true;
        }
    }

    private LocalComputeWorker CreateLocalWorker()
    {
        _logger.LogWarning("No compute worker is available; jobs run inside this runtime");
        return new LocalComputeWorker(new ComputeWorkerHost(_handlers, _generatorFactory));
    }

    private void Dispatch()
    {
        while (TryAssign(out var worker, out var item))
            _ = ServeAsync(worker, item);
    }

    private bool TryAssign([NotNullWhen(true)] out IComputeWorker? worker, [NotNullWhen(true)] out IPoolWorkItem? item)
    {
        lock (_gate)
        {
            worker = null;
            item = null;
            if (_idleWorkers.Count == 0 || !TryDequeuePending(out item))
                return false;

            worker = _idleWorkers.Pop();
            return true;
        }
    }

    private bool TryDequeuePending([NotNullWhen(true)] out IPoolWorkItem? item)
    {
        while (_queue.TryDequeue(out item))
        {
            if (!item.IsCompleted)
                return true;
        }

        return false;
    }

    private async Task ServeAsync(IComputeWorker worker, IPoolWorkItem? item)
    {
        while (item is not null)
        {
            var attempt = item.BeginAttempt();
            try
            {
                item.Complete(await worker.ExecuteAsync(item.Request).ConfigureAwait(false));
            }
            catch (ComputeWorkerException ex)
            {
                _logger.LogWarning(ex, "A compute worker failed and is replaced");
                RetryOrFail(item, attempt, ex);
                Replace(worker);
                Dispatch();
                return;
            }

            item = TakeNextOrPark(worker);
        }
    }

    private void RetryOrFail(IPoolWorkItem item, int attempt, ComputeWorkerException exception)
    {
        if (attempt < MaxAttemptsPerJob)
            _queue.Enqueue(item, item.Priority);
        else
            item.Fail(new ComputeJobException($"The job failed on {attempt} compute workers: {exception.Message}", exception));
    }

    private IPoolWorkItem? TakeNextOrPark(IComputeWorker worker)
    {
        lock (_gate)
        {
            if (_isDisposed)
                return null;

            if (TryDequeuePending(out var item))
                return item;

            _idleWorkers.Push(worker);
            return null;
        }
    }

    private void Replace(IComputeWorker worker)
    {
        bool startsReplacement;
        lock (_gate)
        {
            _workers.Remove(worker);
            startsReplacement = !_isDisposed;
            if (startsReplacement)
                _pendingStarts++;
        }

        worker.Dispose();
        if (startsReplacement)
            _ = StartWorkerAsync();
    }
}
