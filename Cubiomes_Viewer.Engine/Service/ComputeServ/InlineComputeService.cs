using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// Runs jobs one at a time on the caller's context, yielding between jobs so the caller's thread stays responsive.
/// Used where no worker threads exist (single-threaded WASM) and as a deterministic service in tests.
/// </summary>
public sealed class InlineComputeService : IComputeService, IDisposable
{
    public int WorkerCount => 1;

    private readonly JobHandlerRegistry _handlers;
    private readonly WorkerContext _context;
    private readonly ComputeQueue<IComputeWorkItem> _queue = new();
    private readonly object _gate = new();
    private bool _isPumping;
    private bool _isDisposed;

    public InlineComputeService(JobHandlerRegistry handlers, IWorldGeneratorFactory generatorFactory)
    {
        _handlers = handlers;
        _context = new WorkerContext(generatorFactory);
    }

    public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var item = new ComputeWorkItem<TResult>(job, _handlers.Resolve(job), cancellationToken);
        if (Enqueue(item, priority))
            _ = PumpAsync();
        return item.Task;
    }

    /// <summary>Fails the jobs still waiting; the generators are freed once no job runs on them any more.</summary>
    public void Dispose()
    {
        bool isIdle;
        lock (_gate)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            isIdle = !_isPumping;
        }

        foreach (var item in _queue.Complete())
            item.Abandon(new ObjectDisposedException(nameof(InlineComputeService)));
        if (isIdle)
            _context.Dispose();
    }

    /// <summary>Queues the item and says whether the pump has to be started for it.</summary>
    private bool Enqueue(IComputeWorkItem item, JobPriority priority)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            _queue.Enqueue(item, priority);
            if (_isPumping)
                return false;

            _isPumping = true;
            return true;
        }
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            await Task.Yield();

            if (!TryTakeNext(out var item))
                return;

            item.Execute(_context);
        }
    }

    /// <summary>Takes the next job, or stops the pump and frees the generators if the service was disposed while it ran.</summary>
    private bool TryTakeNext([NotNullWhen(true)] out IComputeWorkItem? item)
    {
        lock (_gate)
        {
            if (!_isDisposed && _queue.TryDequeue(out item))
                return true;

            item = null;
            _isPumping = false;
            if (!_isDisposed)
                return false;
        }

        _context.Dispose();
        return false;
    }
}
