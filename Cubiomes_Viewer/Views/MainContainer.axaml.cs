using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.TopLevelServ;
using Cubiomes_Viewer.Service.UiZoomServ;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Views;

/// <summary>
/// Hosts the desktop and the compact shell and keeps the single map view inside whichever shell fits the current size,
/// or inside a window of its own while the map is undocked. Everything inside is scaled by the interface size.
/// </summary>
public sealed partial class MainContainer : UserControl
{
    /// <summary>How long after starting the panels wait to be prepared, so that the start and the first map tiles come first.</summary>
    private static readonly TimeSpan _panelPreparationDelay = TimeSpan.FromSeconds(2);

    private readonly MapHostView _mapHost;
    private readonly DesktopShellView _desktopShell;
    private readonly MobileShellView _mobileShell;
    private readonly MainViewModel _main;
    private readonly MapHostViewModel _mapActions;
    private readonly INotificationService _notificationService;
    private readonly ITopLevelProvider _topLevelProvider;
    private readonly IUiZoomService _uiZoom;
    private TopLevel? _topLevel;
    private OpenPopupStack? _openPopups;
    private MapWindow? _mapWindow;
    private IDisposable? _panelPreparation;
    private bool _isPreparingPanels;
    private bool? _isCompact;

    public MainContainer(MapHostView mapHost, DesktopShellView desktopShell, MobileShellView mobileShell, MainViewModel main, MapHostViewModel mapActions,
        INotificationService notificationService, ITopLevelProvider topLevelProvider, IUiZoomService uiZoom)
    {
        InitializeComponent();

        _mapHost = mapHost;
        _desktopShell = desktopShell;
        _mobileShell = mobileShell;
        _main = main;
        _mapActions = mapActions;
        _notificationService = notificationService;
        _topLevelProvider = topLevelProvider;
        _uiZoom = uiZoom;

        _desktopShell.IsVisible = false;
        _mobileShell.IsVisible = false;
        ShellHost.Children.Insert(0, _desktopShell);
        ShellHost.Children.Insert(1, _mobileShell);
    }

    private Panel ShellMapSlot => _isCompact == true ? _mobileShell.MapHost : _desktopShell.MapHost;

    private PanelHost ShellPanels => _isCompact == true ? _mobileShell.PanelHost : _desktopShell.PanelHost;

    /// <summary>The main window closing takes the map window with it.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _main.PropertyChanged -= OnMainChanged;
        _uiZoom.Changed -= OnUiZoomChanged;
        if (_topLevel is not null)
        {
            _topLevel.BackRequested -= OnBackRequested;
            _topLevel = null;
        }
        _openPopups?.Dispose();
        _openPopups = null;
        _panelPreparation?.Dispose();
        _panelPreparation = null;
        if (_mapWindow is { } window)
            CloseMapWindow(window);
    }

    private void UserControl_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevelProvider.SetTopLevel(_topLevel);
            _topLevel.BackRequested += OnBackRequested;
        }
        _openPopups = new OpenPopupStack();
        _notificationService.Setup(MainWindowNotificationManager);
        _main.PropertyChanged += OnMainChanged;
        _uiZoom.Changed += OnUiZoomChanged;

        ShowZoom();
        UpdateLayoutMode(Bounds.Size);
        ShowMapWindow();
        _panelPreparation = DispatcherTimer.RunOnce(PreparePanels, _panelPreparationDelay);
    }

    /// <summary>Builds the panels of the shown shell while the app is idle, so that each opens at once the first time too.</summary>
    private void PreparePanels()
    {
        _isPreparingPanels = true;
        ShellPanels.PrepareWhenIdle(_main.Panels);
    }

    /// <summary>Back closes the newest menu first, then stops what is under way on the map, then closes the open panel, and only then leaves the app.</summary>
    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (!e.Handled)
            e.Handled = _openPopups?.TryCloseNewest() == true || _mapActions.TryDismiss() || _main.TryClosePanel();
    }

    private void OnUiZoomChanged(object? sender, EventArgs e)
    {
        ShowZoom();
        UpdateLayoutMode(Bounds.Size);
    }

    private void ShowZoom()
    {
        Zoom.LayoutTransform = new ScaleTransform(_uiZoom.Scale, _uiZoom.Scale);
        _mapWindow?.ShowZoom(_uiZoom.Scale);
    }

    private void UserControl_SizeChanged(object? sender, SizeChangedEventArgs e)
        => UpdateLayoutMode(e.NewSize);

    /// <summary>Picks the compact shell when the panels would not fit at the width the interface size leaves them.</summary>
    private void UpdateLayoutMode(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var useCompact = size.Width / _uiZoom.Scale < Constants.CompactLayoutMaxWidth;
        if (_isCompact == useCompact)
            return;

        _isCompact = useCompact;
        if (_mapWindow is null)
            MoveMapTo(ShellMapSlot);

        _desktopShell.IsVisible = !useCompact;
        _desktopShell.IsEnabled = !useCompact;
        _mobileShell.IsVisible = useCompact;
        _mobileShell.IsEnabled = useCompact;
        if (_isPreparingPanels)
            PreparePanels();
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
        window.ShowZoom(_uiZoom.Scale);
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
