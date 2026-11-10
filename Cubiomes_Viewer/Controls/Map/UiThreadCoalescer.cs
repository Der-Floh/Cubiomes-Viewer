using Avalonia.Threading;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Turns notifications from any thread into at most one pending call on the UI thread.</summary>
internal sealed class UiThreadCoalescer
{
    private readonly Action _action;
    private int _isQueued;

    public UiThreadCoalescer(Action action)
    {
        _action = action;
    }

    public void Request()
    {
        if (Interlocked.Exchange(ref _isQueued, 1) != 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            Interlocked.Exchange(ref _isQueued, 0);
            _action();
        });
    }
}
