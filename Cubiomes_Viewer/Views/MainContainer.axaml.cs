using Avalonia;
using Avalonia.Controls;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.TopLevelServ;

namespace Cubiomes_Viewer.Views;

/// <summary>
/// Hosts the desktop and the compact shell and keeps the single map view inside whichever shell fits the current size.
/// </summary>
public sealed partial class MainContainer : UserControl
{
    private readonly MapHostView _mapHost;
    private readonly DesktopShellView _desktopShell;
    private readonly MobileShellView _mobileShell;
    private readonly INotificationService _notificationService;
    private readonly ITopLevelProvider _topLevelProvider;
    private bool? _isCompact;

    public MainContainer(MapHostView mapHost, DesktopShellView desktopShell, MobileShellView mobileShell, INotificationService notificationService, ITopLevelProvider topLevelProvider)
    {
        InitializeComponent();

        _mapHost = mapHost;
        _desktopShell = desktopShell;
        _mobileShell = mobileShell;
        _notificationService = notificationService;
        _topLevelProvider = topLevelProvider;

        _desktopShell.IsVisible = false;
        _mobileShell.IsVisible = false;
        ShellHost.Children.Insert(0, _desktopShell);
        ShellHost.Children.Insert(1, _mobileShell);
    }

    private void UserControl_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null)
            _topLevelProvider.SetTopLevel(topLevel);
        _notificationService.Setup(MainWindowNotificationManager);

        UpdateLayoutMode(Bounds.Size);
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
        MoveMapTo(useCompact ? _mobileShell.MapHost : _desktopShell.MapHost);

        _desktopShell.IsVisible = !useCompact;
        _desktopShell.IsEnabled = !useCompact;
        _mobileShell.IsVisible = useCompact;
        _mobileShell.IsEnabled = useCompact;
    }

    private void MoveMapTo(Panel slot)
    {
        if (_mapHost.Parent is Panel current)
            current.Children.Remove(_mapHost);
        slot.Children.Add(_mapHost);
    }
}
