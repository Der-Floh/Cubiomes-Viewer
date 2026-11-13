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
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Tests.Controls;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

public sealed class MapHostViewTests : IDisposable
{
    private const int ViewSize = 256;
    private const int BytesPerPixel = 4;
    private const int DragDistance = 40;
    private const int PinOffset = 40;
    private const string CalloutTitleClass = "callout-title";
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
    public async Task Click_OnTheSpawnMarker_PicksItDrawsItLargerAndShowsItsCalloutAboveIt()
    {
        var styleService = _services.GetRequiredService<IMapStyleService>();
        var spawn = await ShowSpawnAtTheCenterAsync();
        var beyondTheNormalBadge = (int)(_center.X + styleService.MarkerSize / 2 + 2);

        Click(_center);
        var frame = CaptureFrame();

        Assert.Equal(new MapSelection("World spawn", styleService.Spawn, spawn), _map.Selection);
        var title = GetCalloutTitle();
        Assert.True(title.IsEffectivelyVisible);
        Assert.Equal("World spawn", title.Text);
        Assert.Equal(ToRgb(styleService.Spawn.Fill), FramePixels.Read(frame, beyondTheNormalBadge, (int)_center.Y));

        var callout = GetCallout().Bounds;
        Assert.True(callout.Bottom <= _center.Y - styleService.MarkerSize * SelectionLayer.Magnification / 2 - CalloutPlacement.Gap + 0.5, $"The callout reaches down to {callout.Bottom}.");
        Assert.InRange(callout.Center.X, _center.X - 1, _center.X + 1);
    }

    [AvaloniaFact]
    public async Task Click_BesideTheMarkers_ClosesTheCallout()
    {
        await ShowSpawnAtTheCenterAsync();
        Click(_center);
        var beside = new Point(ViewSize / 4, ViewSize * 3 / 4);
        Assert.Null(_host.GetVisualDescendants().OfType<MapView>().Single().HitTestMarker(beside));

        Click(beside);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(_map.Selection);
        Assert.False(GetCalloutTitle().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task PinToggle_ThenAClick_PutsAPinOnTheClickedBlockAndPicksIt()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();
        _map.IsPlacingPin = true;

        Click(new Point(_center.X + PinOffset, _center.Y + PinOffset));
        var frame = CaptureFrame();

        var pin = Assert.Single(_services.GetRequiredService<IPinService>().GetPins(_map.World.Seed, _map.World.Dimension));
        Assert.Equal(new BlockPos(spawn.X + PinOffset, spawn.Z + PinOffset), new BlockPos(pin.X, pin.Z));
        Assert.False(_map.IsPlacingPin);
        Assert.Same(pin, _map.Selection?.Pin);
        Assert.Equal(MapSelection.PinName, GetCalloutTitle().Text);

        var styleService = _services.GetRequiredService<IMapStyleService>();
        var bounds = styleService.GetPinBounds(new Point(_center.X + PinOffset + 1, _center.Y + PinOffset + 1), SelectionLayer.Magnification);
        var headCenter = new Point(bounds.Center.X, bounds.Top + bounds.Width / 2);
        Assert.Equal(ToRgb(styleService.PlainPin.Fill), FramePixels.Read(frame, (int)(headCenter.X + bounds.Width * 0.42), (int)headCenter.Y));
        Assert.Equal(new RgbColor(255, 255, 255), FramePixels.Read(frame, (int)(headCenter.X + bounds.Width * 0.2), (int)headCenter.Y));
    }

    [AvaloniaFact]
    public async Task Click_OnAPin_PicksIt()
    {
        await ShowSpawnAtTheCenterAsync();
        _map.IsPlacingPin = true;
        Click(new Point(_center.X + PinOffset, _center.Y + PinOffset));
        _map.Selection = null;

        var tip = new Point(_center.X + PinOffset + 0.5, _center.Y + PinOffset + 0.5);
        var head = _services.GetRequiredService<IMapStyleService>().GetPinBounds(tip);
        Click(new Point(head.Center.X, head.Top + head.Width / 2));

        Assert.NotNull(_map.Selection?.Pin);
    }

    [AvaloniaFact]
    public async Task Drag_OnThePickedPin_MovesThePinInsteadOfTheMap()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();
        _map.IsPlacingPin = true;
        Click(new Point(_center.X + PinOffset, _center.Y + PinOffset));
        var pin = Assert.IsType<MapPin>(_map.Selection?.Pin);
        var camera = _map.Camera;
        var tip = new Point(_center.X + PinOffset + 0.5, _center.Y + PinOffset + 0.5);
        var bounds = _services.GetRequiredService<IMapStyleService>().GetPinBounds(tip, SelectionLayer.Magnification);
        var grab = new Point(bounds.Center.X, bounds.Top + bounds.Width / 2);
        var drop = new Point(grab.X + DragDistance, grab.Y);

        _window.MouseDown(grab, MouseButton.Left);
        _window.MouseMove(drop);
        _window.MouseUp(drop, MouseButton.Left);

        Assert.Equal(camera, _map.Camera);
        Assert.Equal((spawn.X + PinOffset + DragDistance, spawn.Z + PinOffset), (pin.X, pin.Z));
        Assert.Same(pin, _map.Selection?.Pin);
        Assert.Equal(new BlockPos(pin.X, pin.Z), _map.Selection?.Position);
    }

    [AvaloniaFact]
    public async Task Drag_OnAPinThatIsNotPicked_MovesTheMap()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();
        _map.IsPlacingPin = true;
        Click(new Point(_center.X + PinOffset, _center.Y + PinOffset));
        var pin = Assert.IsType<MapPin>(_map.Selection?.Pin);
        _map.Selection = null;
        var tip = new Point(_center.X + PinOffset + 0.5, _center.Y + PinOffset + 0.5);
        var head = _services.GetRequiredService<IMapStyleService>().GetPinBounds(tip);
        var grab = new Point(head.Center.X, head.Top + head.Width / 2);

        _window.MouseDown(grab, MouseButton.Left);
        _window.MouseMove(new Point(grab.X + DragDistance, grab.Y));
        _window.MouseUp(new Point(grab.X + DragDistance, grab.Y), MouseButton.Left);

        Assert.Equal(spawn.X - DragDistance, _map.Camera.CenterX);
        Assert.Equal((spawn.X + PinOffset, spawn.Z + PinOffset), (pin.X, pin.Z));
    }

    [AvaloniaFact]
    public void PinToggle_On_ShowsACrosshairOverTheMap()
    {
        var map = _host.GetVisualDescendants().OfType<MapView>().Single();

        _map.IsPlacingPin = true;
        Assert.NotNull(map.Cursor);

        _map.IsPlacingPin = false;
        Assert.Null(map.Cursor);
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
    public async Task RightClick_DuringADrag_NeitherPicksAMarkerNorEndsTheDrag()
    {
        var spawn = await ShowSpawnAtTheCenterAsync();

        _window.MouseDown(_center, MouseButton.Left);
        _window.MouseDown(_center, MouseButton.Right, RawInputModifiers.LeftMouseButton);
        _window.MouseUp(_center, MouseButton.Right, RawInputModifiers.LeftMouseButton);
        _window.MouseMove(new Point(_center.X + DragDistance, _center.Y), RawInputModifiers.LeftMouseButton);
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
    public void Wheel_TurnedToZoomInWithShift_LowersTheYLevelByABiomeCellInsteadOfZooming()
    {
        _session.SampleHeight = 64;
        var camera = _map.Camera;

        _window.MouseWheel(_center, new Vector(0, 1), RawInputModifiers.Shift);

        Assert.Equal(64 - Constants.SampleHeightWheelStep, _session.SampleHeight);
        Assert.Equal(camera, _map.Camera);
    }

    [AvaloniaFact]
    public void Wheel_TurnedToZoomOutWithShiftAsAHorizontalWheel_RaisesTheYLevelUpToTheHighest()
    {
        _session.SampleHeight = Constants.MaxSampleHeight - 1;

        _window.MouseWheel(_center, new Vector(-1, 0), RawInputModifiers.Shift);

        Assert.Equal(Constants.MaxSampleHeight, _session.SampleHeight);
    }

    [AvaloniaFact]
    public void Wheel_WithShiftInSmallTurns_MovesTheYLevelOnceTheyAddUpToANotch()
    {
        _session.SampleHeight = 64;

        _window.MouseWheel(_center, new Vector(0, 0.5), RawInputModifiers.Shift);
        var afterHalf = _session.SampleHeight;
        _window.MouseWheel(_center, new Vector(0, 0.5), RawInputModifiers.Shift);

        Assert.Equal(64, afterHalf);
        Assert.Equal(64 - Constants.SampleHeightWheelStep, _session.SampleHeight);
    }

    [AvaloniaFact]
    public void Wheel_WithoutShift_ZoomsAndKeepsTheYLevel()
    {
        _session.SampleHeight = 64;
        var blocksPerPixel = _map.Camera.BlocksPerPixel;

        _window.MouseWheel(_center, new Vector(0, 1));

        Assert.True(_map.Camera.BlocksPerPixel < blocksPerPixel);
        Assert.Equal(64, _session.SampleHeight);
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
    public async Task HeightStyle_ChangedToGrayscale_RepaintsTheLoadedTilesWithoutColour()
    {
        _session.Layer = LayerMode.Height;
        _map.ShowGrid = false;
        _map.Camera = new MapCamera(0, 0, 1);
        Dispatcher.UIThread.RunJobs();
        var tileService = _services.GetRequiredService<IMapTileService>();
        var content = MapContent.For(_map.World, LayerMode.Height, _map.SampleHeight, octaves: 0);
        await TestWait.UntilAsync(() => tileService.TryGetCachedValue(content, 8, 8, out _) && tileService.PendingCount == 0, "The height tiles were not generated in time.");

        var coloured = CapturePixel(ViewSize / 2 + 8, ViewSize / 2 + 8);
        _map.HeightStyle = HeightStyle.Grayscale;
        var gray = CapturePixel(ViewSize / 2 + 8, ViewSize / 2 + 8);

        Assert.False(coloured.Red == coloured.Green && coloured.Green == coloured.Blue, $"The height colour is {coloured}.");
        Assert.True(gray.Red == gray.Green && gray.Green == gray.Blue, $"The grayscale pixel is {gray}.");
    }

    [AvaloniaFact]
    public async Task StructurePotential_OverABiomeWithoutTheStructure_DimsItWithoutNewTiles()
    {
        _map.ShowGrid = false;
        _map.Camera = new MapCamera(0, 0, 1);
        Dispatcher.UIThread.RunJobs();
        var tileService = _services.GetRequiredService<IMapTileService>();
        var content = MapContent.For(_map.World, LayerMode.Biomes, _map.SampleHeight, octaves: 0);
        await TestWait.UntilAsync(() => tileService.TryGetCachedBiome(content, 8, 8, out _) && tileService.PendingCount == 0, "The biome tiles were not generated in time.");
        var coloured = CapturePixel(ViewSize / 2 + 8, ViewSize / 2 + 8);

        _session.Layer = LayerMode.StructurePotential;
        var dimmed = CapturePixel(ViewSize / 2 + 8, ViewSize / 2 + 8);

        Assert.Equal(0, tileService.PendingCount);
        Assert.False(coloured.Red == coloured.Green && coloured.Green == coloured.Blue, $"The mushroom fields are {coloured}.");
        Assert.True(dimmed.Red == dimmed.Green && dimmed.Green == dimmed.Blue, $"The dimmed pixel is {dimmed}.");
        Assert.StartsWith("Mushroom Fields", _map.HoverText.Length > 0 ? _map.HoverText : HoverAtTheCenter(), StringComparison.Ordinal);
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

    private (int Red, int Green, int Blue) CapturePixel(int x, int y)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = _window.CaptureRenderedFrame();
        return ReadPixel(Assert.IsType<WriteableBitmap>(frame, exactMatch: false), x, y);
    }

    private string HoverAtTheCenter()
    {
        _window.MouseMove(new Point(_center.X + 8, _center.Y + 8));
        return _map.HoverText;
    }

    private WriteableBitmap CaptureFrame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return Assert.IsType<WriteableBitmap>(_window.CaptureRenderedFrame(), exactMatch: false);
    }

    private TextBlock GetCalloutTitle()
        => _host.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains(CalloutTitleClass));

    private SelectionCalloutView GetCallout() => _host.GetVisualDescendants().OfType<SelectionCalloutView>().Single();

    private static RgbColor ToRgb(Avalonia.Media.IBrush brush)
    {
        var color = Assert.IsAssignableFrom<Avalonia.Media.ISolidColorBrush>(brush).Color;
        return new RgbColor(color.R, color.G, color.B);
    }

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
