namespace Cubiomes_Viewer.Engine.Tests;

/// <summary>
/// Runs asynchronous code the way a UI thread does: on one thread that takes one continuation after the other,
/// which makes the order of everything that happens in a test certain.
/// </summary>
internal static class SingleThread
{
    public static void Run(Func<Task> action)
    {
        var previous = SynchronizationContext.Current;
        var pump = new Pump();
        SynchronizationContext.SetSynchronizationContext(pump);
        try
        {
            var task = action();
            task.ContinueWith(_ => pump.Complete(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            pump.Run();
            task.GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Once the test is over the thread takes no more work, as when an app closes, so later posts are dropped.</summary>
    private sealed class Pump : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = [];
        private readonly object _gate = new();
        private bool _completed;

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_gate)
            {
                if (_completed)
                    return;

                _posted.Enqueue((d, state));
                Monitor.Pulse(_gate);
            }
        }

        public void Run()
        {
            while (TryTake(out var item))
                item.Callback(item.State);
        }

        public void Complete()
        {
            lock (_gate)
            {
                _completed = true;
                Monitor.PulseAll(_gate);
            }
        }

        private bool TryTake(out (SendOrPostCallback Callback, object? State) item)
        {
            lock (_gate)
            {
                while (_posted.Count == 0 && !_completed)
                    Monitor.Wait(_gate);
                return _posted.TryDequeue(out item);
            }
        }
    }
}
