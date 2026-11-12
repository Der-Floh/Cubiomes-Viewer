using Avalonia;
using Avalonia.Controls;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

namespace Cubiomes_Viewer.Views;

public sealed partial class MainWindow : Window
{
    private readonly ISettingsService _settingsService;
    private WindowPlacement? _normalPlacement;

    /// <param name="container">The content of the window.</param>
    /// <param name="settingsService">Settings that are loaded already, so that the window opens where it was closed.</param>
    public MainWindow(MainContainer container, ISettingsService settingsService)
    {
        InitializeComponent();
        Content = container;
        _settingsService = settingsService;
        RestorePlacement();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RememberNormalPlacement();
        PositionChanged += OnPositionChanged;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        PositionChanged -= OnPositionChanged;

        var session = _settingsService.Current.Session;
        session.Window = session.RememberWindow && _normalPlacement is { } placement
            ? new WindowPlacement { X = placement.X, Y = placement.Y, Width = placement.Width, Height = placement.Height, IsMaximized = WindowState == WindowState.Maximized }
            : null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClientSizeProperty || change.Property == WindowStateProperty)
            RememberNormalPlacement();
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e) => RememberNormalPlacement();

    /// <summary>Keeps the size and position the window has while it is neither maximized nor minimized.</summary>
    private void RememberNormalPlacement()
    {
        if (WindowState != WindowState.Normal || !IsVisible)
            return;

        _normalPlacement = new WindowPlacement { X = Position.X, Y = Position.Y, Width = ClientSize.Width, Height = ClientSize.Height };
    }

    private void RestorePlacement()
    {
        var session = _settingsService.Current.Session;
        if (!session.RememberWindow || session.Window is not { } placement || placement.Width < MinWidth || placement.Height < MinHeight)
            return;

        Width = placement.Width;
        Height = placement.Height;
        var position = new PixelPoint(placement.X, placement.Y);
        if (Screens.ScreenFromPoint(position) is not null)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = position;
        }

        if (placement.IsMaximized)
            WindowState = WindowState.Maximized;
    }
}
