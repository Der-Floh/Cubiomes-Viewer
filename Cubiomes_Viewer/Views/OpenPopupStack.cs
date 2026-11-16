using Avalonia;
using Avalonia.Controls.Primitives;

namespace Cubiomes_Viewer.Views;

/// <summary>
/// Keeps the open popups, such as menus, flyouts and drop-downs, in the order they opened, so that back can close the newest first.
/// </summary>
internal sealed class OpenPopupStack : IDisposable
{
    private readonly List<Popup> _open = [];
    private readonly IDisposable _subscription;

    public OpenPopupStack() => _subscription = Popup.IsOpenProperty.Changed.AddClassHandler<Popup>(OnIsOpenChanged);

    /// <summary>Closes the popup that opened last; false when none is open.</summary>
    public bool TryCloseNewest()
    {
        if (_open.Count == 0)
            return false;

        var newest = _open[^1];
        _open.RemoveAt(_open.Count - 1);
        newest.SetCurrentValue(Popup.IsOpenProperty, false);
        return true;
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _open.Clear();
    }

    private void OnIsOpenChanged(Popup popup, AvaloniaPropertyChangedEventArgs e)
    {
        _open.Remove(popup);
        if (popup.IsOpen)
            _open.Add(popup);
    }
}
