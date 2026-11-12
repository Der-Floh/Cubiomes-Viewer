using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.TopLevelServ;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

/// <summary>
/// Hosts the desktop and the compact shell and keeps the single map view inside whichever shell fits the current size,
/// or inside a window of its own while the map is undocked.
/// </summary>
public sealed partial class MainContainer : UserControl
{
    private readonly MapHostView _mapHost;
    private readonly DesktopShellView _desktopShell;
    private readonly MobileShellView _mobileShell;
    private readonly MainViewModel _main;
    private readonly INotificationService _notificationService;
    private readonly ITopLevelProvider _topLevelProvider;
    private MapWindow? _mapWindow;
    private bool? _isCompact;

    public MainContainer(MapHostView mapHost, DesktopShellView desktopShell, MobileShellView mobileShell, MainViewModel main, INotificationService notificationService, ITopLevelProvider topLevelProvider)
    {
        InitializeComponent();

        _mapHost = mapHost;
        _desktopShell = desktopShell;
        _mobileShell = mobileShell;
        _main = main;
        _notificationService = notificationService;
        _topLevelProvider = topLevelProvider;

        _desktopShell.IsVisible = false;
        _mobileShell.IsVisible = false;
        ShellHost.Children.Insert(0, _desktopShell);
        ShellHost.Children.Insert(1, _mobileShell);
    }

    private Panel ShellMapSlot => _isCompact == true ? _mobileShell.MapHost : _desktopShell.MapHost;

    /// <summary>The main window closing takes the map window with it.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _main.PropertyChanged -= OnMainChanged;
        if (_mapWindow is { } window)
            CloseMapWindow(window);
    }

    private void UserControl_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
            _topLevelProvider.SetTopLevel(topLevel);
        _notificationService.Setup(MainWindowNotificationManager);
        _main.PropertyChanged += OnMainChanged;

        UpdateLayoutMode(Bounds.Size);
        ShowMapWindow();
    }

    private void UserControl_SizeChanged(object? sender, SizeChangedEventArgs e)
        => UpdateLayoutMode(e.NewSize);

    private void UpdateLayoutMode(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var useCompact = size.Width < Constants.CompactLayoutMaxWidth;
        if (_isCompact == useCompact)
            return;

        _isCompact = useCompact;
        if (_mapWindow is null)
            MoveMapTo(ShellMapSlot);

        _desktopShell.IsVisible = !useCompact;
        _desktopShell.IsEnabled = !useCompact;
        _mobileShell.IsVisible = useCompact;
        _mobileShell.IsEnabled = useCompact;
    }

    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsMapUndocked))
            ShowMapWindow();
    }

    /// <summary>Moves the map into a window of its own while it is undocked, and back next to the panels when it is not.</summary>
    private void ShowMapWindow()
    {
        if (_main.IsMapUndocked && _mapWindow is null)
            OpenMapWindow();
        else if (!_main.IsMapUndocked && _mapWindow is { } window)
            CloseMapWindow(window);
    }

    private void OpenMapWindow()
    {
        var window = new MapWindow { DataContext = _main.Map };
        MoveMapTo(window.MapHost);
        window.Closed += OnMapWindowClosed;
        _mapWindow = window;
        window.Show();
    }

    private void CloseMapWindow(MapWindow window)
    {
        window.Closed -= OnMapWindowClosed;
        _mapWindow = null;
        MoveMapTo(ShellMapSlot);
        window.Close();
    }

    /// <summary>Closing the map window brings the map back.</summary>
    private void OnMapWindowClosed(object? sender, EventArgs e)
    {
        if (_mapWindow is { } window)
        {
            window.Closed -= OnMapWindowClosed;
            _mapWindow = null;
            MoveMapTo(ShellMapSlot);
        }

        _main.IsMapUndocked = false;
    }

    private void MoveMapTo(Panel slot)
    {
        if (_mapHost.Parent is Panel current)
            current.Children.Remove(_mapHost);
        slot.Children.Add(_mapHost);
    }
}
