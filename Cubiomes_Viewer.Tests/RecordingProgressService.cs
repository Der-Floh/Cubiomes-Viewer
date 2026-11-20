using Cubiomes_Viewer.Service.ProgressServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>
/// The real progress service, counting the scopes begun and ended and recording each share shown, since a scope that never
/// reports can only be seen in the counts.
/// </summary>
internal sealed class RecordingProgressService : IProgressService
{
    private readonly ProgressService _service = new();
    private readonly Lock _gate = new();
    private readonly List<double?> _shares = [];
    private int _begun;
    private int _ended;

    public RecordingProgressService() => _service.Changed += OnChanged;

    public double? Share => _service.Share;

    public int Begun => Volatile.Read(ref _begun);

    public int Ended => Volatile.Read(ref _ended);

    public IReadOnlyList<double?> Shares
    {
        get
        {
            lock (_gate)
                return [.. _shares];
        }
    }

    public event EventHandler? Changed;

    public IProgressScope Begin()
    {
        Interlocked.Increment(ref _begun);
        return new CountedScope(_service.Begin(), this);
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        lock (_gate)
            _shares.Add(_service.Share);
        Changed?.Invoke(this, e);
    }

    private sealed class CountedScope(IProgressScope scope, RecordingProgressService service) : IProgressScope
    {
        private int _isEnded;

        public void Report(double value) => scope.Report(value);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isEnded, 1) == 0)
                Interlocked.Increment(ref service._ended);
            scope.Dispose();
        }
    }
}
