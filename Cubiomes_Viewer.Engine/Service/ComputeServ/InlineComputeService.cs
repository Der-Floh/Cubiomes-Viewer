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

    public InlineComputeService(JobHandlerRegistry handlers, IWorldGeneratorFactory generatorFactory)
    {
        _handlers = handlers;
        _context = new WorkerContext(generatorFactory);
    }

    public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        var item = new ComputeWorkItem<TResult>(job, _handlers.Resolve(job), cancellationToken);
        _queue.Enqueue(item, priority);
        StartPump();
        return item.Task;
    }

    public void Dispose() => _context.Dispose();

    private void StartPump()
    {
        lock (_gate)
        {
            if (_isPumping)
                return;
            _isPumping = true;
        }

        _ = PumpAsync();
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            await Task.Yield();

            IComputeWorkItem? item;
            lock (_gate)
            {
                if (!_queue.TryDequeue(out item))
                {
                    _isPumping = false;
                    return;
                }
            }

            item.Execute(_context);
        }
    }
}
