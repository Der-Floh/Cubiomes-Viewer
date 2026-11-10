using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>A thread-safe priority queue of work items; equal priorities run in submission order.</summary>
internal sealed class ComputeQueue
{
    private static readonly Comparer<(JobPriority Priority, long Sequence)> _order = Comparer<(JobPriority Priority, long Sequence)>.Create(
        static (left, right) =>
        {
            var byPriority = left.Priority.CompareTo(right.Priority);
            return byPriority != 0 ? byPriority : left.Sequence.CompareTo(right.Sequence);
        });

    private readonly PriorityQueue<IComputeWorkItem, (JobPriority Priority, long Sequence)> _items = new(_order);
    private readonly object _gate = new();
    private long _sequence;
    private bool _completed;

    public void Enqueue(IComputeWorkItem item, JobPriority priority)
    {
        lock (_gate)
        {
            _items.Enqueue(item, (priority, _sequence++));
            Monitor.Pulse(_gate);
        }
    }

    public bool TryDequeue([NotNullWhen(true)] out IComputeWorkItem? item)
    {
        lock (_gate)
            return _items.TryDequeue(out item, out _);
    }

    /// <summary>Blocks until an item is available. Returns <see langword="false"/> once the queue was completed.</summary>
    public bool WaitDequeue([NotNullWhen(true)] out IComputeWorkItem? item)
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

    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            Monitor.PulseAll(_gate);
        }
    }
}
