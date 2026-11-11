using System.Collections.Concurrent;

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
        using var pump = new Pump();
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

    private sealed class Pump : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _posted = [];

        public override void Post(SendOrPostCallback d, object? state) => _posted.Add((d, state));

        public void Run()
        {
            foreach (var (callback, state) in _posted.GetConsumingEnumerable())
                callback(state);
        }

        public void Complete() => _posted.CompleteAdding();

        public void Dispose() => _posted.Dispose();
    }
}
