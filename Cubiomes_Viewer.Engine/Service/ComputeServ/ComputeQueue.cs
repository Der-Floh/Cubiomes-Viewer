using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>A thread-safe priority queue of work items; equal priorities run in submission order.</summary>
internal sealed class ComputeQueue<TItem>
    where TItem : class
{
    private static readonly Comparer<(JobPriority Priority, long Sequence)> _order = Comparer<(JobPriority Priority, long Sequence)>.Create(
        static (left, right) =>
        {
            var byPriority = left.Priority.CompareTo(right.Priority);
            return byPriority != 0 ? byPriority : left.Sequence.CompareTo(right.Sequence);
        });

    private readonly PriorityQueue<TItem, (JobPriority Priority, long Sequence)> _items = new(_order);
    private readonly object _gate = new();
    private long _sequence;
    private bool _completed;

    public void Enqueue(TItem item, JobPriority priority)
    {
        lock (_gate)
        {
            _items.Enqueue(item, (priority, _sequence++));
            Monitor.Pulse(_gate);
        }
    }

    public bool TryDequeue([NotNullWhen(true)] out TItem? item)
    {
        lock (_gate)
            return _items.TryDequeue(out item, out _);
    }

    /// <summary>Blocks until an item is available. Returns <see langword="false"/> once the queue was completed.</summary>
    public bool WaitDequeue([NotNullWhen(true)] out TItem? item)
    {
        lock (_gate)
        {
            while (true)
            {
                if (_completed)
                {
                    item = null;
                    return false;
                }

                if (_items.TryDequeue(out item, out _))
                    return true;

                Monitor.Wait(_gate);
            }
        }
    }

    /// <summary>Stops handing out items and returns the ones still waiting, which no worker takes now.</summary>
    public IReadOnlyList<TItem> Complete()
    {
        lock (_gate)
        {
            _completed = true;
            List<TItem> waiting = [];
            while (_items.TryDequeue(out var item, out _))
                waiting.Add(item);
            Monitor.PulseAll(_gate);
            return waiting;
        }
    }
}
