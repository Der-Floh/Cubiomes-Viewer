using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.ViewHistoryServ;

/// <summary>Keeps the steps of the view history in memory, for the apps that have no browser history.</summary>
public sealed class ViewHistoryService : IViewHistoryService
{
    private readonly List<MapLink> _steps = [];
    private int _current = -1;
    private bool _isStepPending;

    public bool CanGoBack => _current > 0;

    public bool CanGoForward => _current >= 0 && _current < _steps.Count - 1;

    public event Action? Changed;

    public event Action<MapLink>? Moved;

    public void Leave(MapLink view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_current < 0)
        {
            _steps.Add(view);
            _current = 0;
        }
        else
        {
            _steps[_current] = view;
            _steps.RemoveRange(_current + 1, _steps.Count - _current - 1);
        }

        _isStepPending = true;
        Changed?.Invoke();
    }

    public void Show(MapLink view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_current < 0 || _isStepPending)
        {
            _steps.Add(view);
            _current = _steps.Count - 1;
            _isStepPending = false;
            Changed?.Invoke();
            return;
        }

        _steps[_current] = view;
    }

    public void GoBack()
    {
        if (CanGoBack)
            MoveTo(_current - 1);
    }

    public void GoForward()
    {
        if (CanGoForward)
            MoveTo(_current + 1);
    }

    private void MoveTo(int step)
    {
        _current = step;
        _isStepPending = false;
        Changed?.Invoke();
        Moved?.Invoke(_steps[step]);
    }
}
