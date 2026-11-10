using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>
/// Loads the data for the requests a view currently needs through the compute service, keeps it in a cache
/// and cancels work for requests that are no longer needed. A request whose job fails is not tried again.
/// </summary>
public sealed class VisibleSetLoader<TRequest, TData>
    where TRequest : notnull
{
    /// <summary>Raised when data became available or the number of pending requests changed. May fire on a worker thread.</summary>
    public event Action? StateChanged;

    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _pending.Count;
        }
    }

    private readonly IComputeService _computeService;
    private readonly LruCache<TRequest, TData> _cache;
    private readonly Func<TRequest, ComputeJob<TData>> _createJob;
    private readonly JobPriorityClass _priorityClass;
    private readonly ILogger _logger;
    private readonly Dictionary<TRequest, CancellationTokenSource> _pending = [];
    private readonly HashSet<TRequest> _failed = [];
    private readonly Lock _gate = new();

    public VisibleSetLoader(IComputeService computeService, LruCache<TRequest, TData> cache, Func<TRequest, ComputeJob<TData>> createJob, JobPriorityClass priorityClass, ILogger logger)
    {
        _computeService = computeService;
        _cache = cache;
        _createJob = createJob;
        _priorityClass = priorityClass;
        _logger = logger;
    }

    public bool TryGet(TRequest request, [MaybeNullWhen(false)] out TData data) => _cache.TryGet(request, out data);

    /// <summary>
    /// Declares the requests currently needed, most important first. Missing data is loaded in that order;
    /// pending requests that are no longer listed are cancelled.
    /// </summary>
    public void SetVisible(IReadOnlyList<TRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        List<(TRequest Request, int Order, CancellationTokenSource Cancellation)> started = [];
        lock (_gate)
        {
            var wanted = requests.ToHashSet();
            foreach (var (request, cancellation) in _pending.Where(pending => !wanted.Contains(pending.Key)).ToArray())
            {
                _pending.Remove(request);
                cancellation.Cancel();
            }

            for (var order = 0; order < requests.Count; order++)
            {
                var request = requests[order];
                if (_pending.ContainsKey(request) || _failed.Contains(request) || _cache.Contains(request))
                    continue;

                var cancellation = new CancellationTokenSource();
                _pending.Add(request, cancellation);
                started.Add((request, order, cancellation));
            }
        }

        foreach (var (request, order, cancellation) in started)
            _ = LoadAsync(request, order, cancellation);

        StateChanged?.Invoke();
    }

    private async Task LoadAsync(TRequest request, int order, CancellationTokenSource cancellation)
    {
        try
        {
            var data = await _computeService.RunAsync(_createJob(request), new JobPriority(_priorityClass, order), cancellation.Token).ConfigureAwait(false);
            _cache.Add(request, data);
        }
        catch (OperationCanceledException)
        {
            // The request left the visible set before its data was loaded.
        }
        catch (Exception ex) when (ex is CubiomesNativeException or ComputeJobException)
        {
            _logger.LogWarning(ex, "{Request} could not be generated and is skipped", request);
            lock (_gate)
                _failed.Add(request);
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.TryGetValue(request, out var current) && current == cancellation)
                    _pending.Remove(request);
            }

            cancellation.Dispose();
            StateChanged?.Invoke();
        }
    }
}
