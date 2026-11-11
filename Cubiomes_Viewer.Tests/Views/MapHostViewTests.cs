using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

public sealed class MapHostViewTests : IDisposable
{
    private const int ViewSize = 256;
    private const int BytesPerPixel = 4;
    private const int SelectionRingRadius = 16;
    private const int DragDistance = 40;
    private const int AntialiasingTolerance = 24;
    private const string CardTitleClass = "card-title";
    private const string LegendTitleClass = "legend-title";
    private static readonly Point _center = new(ViewSize / 2, ViewSize / 2);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly MapHostView _host;
    private readonly Window _window;

    public MapHostViewTests()
    {
        _session = _services.GetRequiredService<WorldSession>();
        _session.Seed = 262;
        _map = _services.GetRequiredService<MapViewModel>();
        _map.SmoothMotion = false;
        _host = _services.GetRequiredService<MapHostView>();
        _window = new Window { Width = ViewSize, Height = ViewSize, Content = _host };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public async Task Click_OnTheSpawnMarker_PicksItAndShowsItsCardAndARing()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();

        Click(_center);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = _window.CaptureRenderedFrame();

        Assert.Equal(new MapSelection("World spawn", _services.GetRequiredService<IMapStyleService>().Spawn, spawn), _map.Selection);
        var title = GetCardTitle();
        Assert.True(title.IsEffectivelyVisible);
        Assert.Equal("World spawn", title.Text);
        Assert.NotNull(frame);
        var ring = _services.GetRequiredService<IMapStyleService>().SelectionColor;
        var (red, green, blue) = ReadPixel(frame, ViewSize / 2 + SelectionRingRadius, ViewSize / 2);
        Assert.InRange(red, ring.R - AntialiasingTolerance, ring.R);
        Assert.InRange(green, ring.G - AntialiasingTolerance, ring.G);
        Assert.InRange(blue, ring.B - AntialiasingTolerance, ring.B);
    }

    [AvaloniaFact]
    public async Task Click_BesideTheMarkers_ClosesTheCard()
    {
        await ShowSpawnAtTheCenterAsync();
        Click(_center);

        Click(new Point(ViewSize / 4, ViewSize / 4));
        Dispatcher.UIThread.RunJobs();

        Assert.Null(_map.Selection);
        Assert.False(GetCardTitle().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Drag_StartedOnAMarker_MovesTheMapWithoutPickingTheMarker()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();

        _window.MouseDown(_center, MouseButton.Left);
        _window.MouseMove(new Point(_center.X + DragDistance, _center.Y));
        _window.MouseUp(new Point(_center.X + DragDistance, _center.Y), MouseButton.Left);

        Assert.Null(_map.Selection);
        Assert.Equal(spawn.X - DragDistance, _map.Camera.CenterX);
    }

    [AvaloniaFact]
    public async Task RightClick_OnTheMap_PointsTheContextMenuAtTheClickedBlock()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();
        var position = new Point(_center.X + 10, _center.Y + 20);

        _window.MouseDown(position, MouseButton.Right);
        _window.MouseUp(position, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        var menu = _services.GetRequiredService<MapMenuViewModel>();
        Assert.Equal(new BlockPos(spawn.X + 10, spawn.Z + 20), menu.Position);
        Assert.Null(_map.Selection);
    }

    [AvaloniaFact]
    public void RightClick_OnTheMap_GivesItTheKeyboardFocus()
    {
        var map = _host.GetVisualDescendants().OfType<MapView>().Single();
        Assert.False(map.IsFocused);

        _window.MouseDown(_center, MouseButton.Right);

        Assert.True(map.IsFocused);
    }

    [AvaloniaFact]
    public void Escape_WhileMeasuring_EndsTheMeasurement()
    {
        Click(_center);
        _map.MeasureStart = new BlockPos(0, 0);

        _window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(_map.IsMeasuring);
    }

    [AvaloniaFact]
    public async Task Hover_OverALayerOfNumbers_ShowsItsValueAndItsLegend()
    {
        _session.Layer = LayerMode.Continentalness;
        _map.Camera = new MapCamera(0, 0, 1);
        Dispatcher.UIThread.RunJobs();
        var tileService = _services.GetRequiredService<IMapTileService>();
        var content = MapContent.For(_map.World, LayerMode.Continentalness, _map.SampleHeight, octaves: 0);
        await TestWait.UntilAsync(() => tileService.TryGetCachedValue(content, 0, 0, out _), "The continentalness tile was not generated in time.");

        _window.MouseMove(_center);

        var legendTitle = _host.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains(LegendTitleClass));
        Assert.StartsWith("Continentalness -1.", _map.HoverText, StringComparison.Ordinal);
        Assert.True(legendTitle.IsEffectivelyVisible);
        Assert.Equal("Continentalness", legendTitle.Text);
    }

    [AvaloniaFact]
    public async Task Click_OnAnEndGateway_PicksItWhileGatewayLinksAreShown()
    {
        _session.Dimension = Dimension.End;
        var gateway = await ShowFirstGatewayAtTheCenterAsync();

        Click(_center);
        Assert.Equal("End gateway 1", _map.Selection?.Name);
        Assert.Equal(gateway.Position, _map.Selection?.Position);

        _map.Selection = null;
        _map.ShowGatewayLinks = false;
        Click(_center);
        Assert.Null(_map.Selection);
    }

    private void Click(Point position)
    {
        _window.MouseDown(position, MouseButton.Left);
        _window.MouseUp(position, MouseButton.Left);
    }

    private async Task<BlockPos> ShowSpawnAtTheCenterAsync()
    {
        var landmarkService = _services.GetRequiredService<IWorldLandmarkService>();
        await TestWait.UntilAsync(() => landmarkService.TryGetLandmarks(_map.World, out _), "The landmarks were not found in time.");
        landmarkService.TryGetLandmarks(_map.World, out var landmarks);

        var spawn = Assert.NotNull(landmarks?.Spawn);
        ShowAtTheCenter(spawn);
        return spawn;
    }

    private async Task<EndGateway> ShowFirstGatewayAtTheCenterAsync()
    {
        var gatewayService = _services.GetRequiredService<IEndGatewayService>();
        await TestWait.UntilAsync(() => gatewayService.TryGetGateways(_map.World, out _), "The end gateways were not found in time.");
        gatewayService.TryGetGateways(_map.World, out var gateways);

        Assert.NotNull(gateways);
        var gateway = gateways.Gateways[0];
        ShowAtTheCenter(gateway.Position);
        return gateway;
    }

    private void ShowAtTheCenter(BlockPos position)
    {
        _map.Camera = new MapCamera(position.X, position.Z, 1);
        Dispatcher.UIThread.RunJobs();
    }

    private TextBlock GetCardTitle()
        => _host.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains(CardTitleClass));

    private static (int Red, int Green, int Blue) ReadPixel(WriteableBitmap bitmap, int x, int y)
    {
        using var buffer = bitmap.Lock();
        var offset = y * buffer.RowBytes + x * BytesPerPixel;
        var first = Marshal.ReadByte(buffer.Address, offset);
        var green = Marshal.ReadByte(buffer.Address, offset + 1);
        var third = Marshal.ReadByte(buffer.Address, offset + 2);

        return buffer.Format == PixelFormat.Bgra8888 ? (third, green, first) : (first, green, third);
    }
}
