namespace Cubiomes_Viewer.Service.ProgressServ;

/// <summary>
/// Keeps the actions that measure their progress while they run and shows the newest one that has reported, so an action
/// that cannot tell how far it is never hides one that can. When the action shown ends, the one before it shows again at
/// the share it last reported.
/// </summary>
public sealed class ProgressService : IProgressService
{
    private readonly Lock _gate = new();
    private readonly List<Scope> _running = [];
    private double? _shown;

    public double? Share
    {
        get
        {
            lock (_gate)
                return _shown;
        }
    }

    public event EventHandler? Changed;

    public IProgressScope Begin()
    {
        var scope = new Scope(this);
        lock (_gate)
            _running.Add(scope);
        return scope;
    }

    private void Report(Scope scope, double share)
    {
        if (!double.IsNaN(share))
            Change(scope, () => scope.Share = Math.Clamp(share, 0, 1));
    }

    private void End(Scope scope) => Change(scope, () =>
    {
        scope.IsEnded = true;
        _running.Remove(scope);
    });

    /// <summary>Changes a scope that has not ended yet, and tells about it when that moves the share shown.</summary>
    private void Change(Scope scope, Action change)
    {
        bool isMoved;
        lock (_gate)
        {
            if (scope.IsEnded)
                return;

            change();
            var shown = _running.LastOrDefault(running => running.Share is not null)?.Share;
            isMoved = shown != _shown;
            _shown = shown;
        }

        if (isMoved)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Scope(ProgressService service) : IProgressScope
    {
        public double? Share { get; set; }

        public bool IsEnded { get; set; }

        public void Report(double value) => service.Report(this, value);

        public void Dispose() => service.End(this);
    }
}
