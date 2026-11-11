using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Controls;

public sealed class MapViewRenderTests
{
    private const int MushroomFieldsBiomeId = 14;
    private const int SampleHeight = 63;
    private const int ViewSize = 256;
    private const int BytesPerPixel = 4;
    private const int MarkerFillOffset = 9;
    private const int MeasuredBlocks = 80;
    private const int LabelBackingOffset = 13;
    private const int BackingOnWhite = 51;
    private const int ColourTolerance = 8;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    public async Task Render_SeedWithMushroomFieldsAtOrigin_DrawsThatBiomeColourAtTheCentre()
    {
        var world = new WorldKey(262, McVersion.V1_18_2, Dimension.Overworld, GeneratorFlags.None);
        var map = new MapView { World = world, SampleHeight = SampleHeight, Camera = new MapCamera(0, 0, 1), SmoothMotion = false };
        map.AddLayer(App.Services.GetRequiredService<TileLayer>());
        map.AddLayer(App.Services.GetRequiredService<GridLayer>());
        var window = new Window { Width = ViewSize, Height = ViewSize, Content = map };
        window.Show();

        await WaitForTileAsync(new TileRequest(world, LayerMode.Biomes, SampleHeight, new TileKey(1, 0, 0)));
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        window.Close();

        Assert.NotNull(frame);
        var expected = App.Services.GetRequiredService<IBiomeCatalog>().DefaultPalette[MushroomFieldsBiomeId];
        Assert.Equal(expected, ReadPixel(frame, ViewSize / 2 + 8, ViewSize / 2 + 8));
    }

    [AvaloniaFact]
    public async Task Render_LandmarksShown_DrawsTheSpawnMarkerAtTheSpawnPoint()
    {
        var world = new WorldKey(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var landmarkService = App.Services.GetRequiredService<IWorldLandmarkService>();
        var map = new MapView { World = world, SampleHeight = SampleHeight, Camera = new MapCamera(0, 0, 1), SmoothMotion = false };
        map.AddLayer(App.Services.GetRequiredService<LandmarkLayer>());
        var window = new Window { Width = ViewSize, Height = ViewSize, Content = map };
        window.Show();

        var spawn = await WaitForSpawnAsync(landmarkService, world);
        map.Camera = new MapCamera(spawn.X, spawn.Z, 1);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        window.Close();

        Assert.NotNull(frame);
        var fill = Assert.IsAssignableFrom<ISolidColorBrush>(App.Services.GetRequiredService<IMapStyleService>().Spawn.Fill).Color;
        Assert.Equal(new BiomeColor(fill.R, fill.G, fill.B), ReadPixel(frame, ViewSize / 2 - MarkerFillOffset, ViewSize / 2));
    }

    [AvaloniaFact]
    public void Render_MeasuredDistance_DrawsTheLineAndItsLengthOnADarkBacking()
    {
        var measure = App.Services.GetRequiredService<MeasureLayer>();
        measure.Start = new BlockPos(-MeasuredBlocks / 2, 0);
        measure.End = new BlockPos(MeasuredBlocks / 2, 0);
        var map = new MapView { Background = Brushes.White, Camera = new MapCamera(0.5, 0.5, 1), SmoothMotion = false };
        map.AddLayer(measure);
        var window = new Window { Width = ViewSize, Height = ViewSize, Content = map };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        window.Close();

        Assert.NotNull(frame);
        var line = App.Services.GetRequiredService<IMapStyleService>().MeasureColor;
        Assert.Equal(new BiomeColor(line.R, line.G, line.B), ReadPixel(frame, ViewSize / 2, ViewSize / 2));
        var backing = ReadPixel(frame, ViewSize / 2 + MeasuredBlocks / 2, ViewSize / 2 - LabelBackingOffset);
        Assert.InRange(backing.Red, BackingOnWhite - ColourTolerance, BackingOnWhite + ColourTolerance);
        Assert.InRange(backing.Green, BackingOnWhite - ColourTolerance, BackingOnWhite + ColourTolerance);
        Assert.InRange(backing.Blue, BackingOnWhite - ColourTolerance, BackingOnWhite + ColourTolerance);
    }

    [AvaloniaFact]
    public void CapturePng_MapShown_ReturnsAnImageOfTheMapSize()
    {
        var host = App.Services.GetRequiredService<MapHostView>();
        var window = new Window { Width = ViewSize, Height = ViewSize, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var png = host.CapturePng();
        window.Close();

        using var image = new Bitmap(new MemoryStream(png));
        Assert.Equal(new PixelSize(ViewSize, ViewSize), image.PixelSize);
    }

    private static async Task<BlockPos> WaitForSpawnAsync(IWorldLandmarkService landmarkService, WorldKey world)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (true)
        {
            if (landmarkService.TryGetLandmarks(world, out var landmarks))
                return Assert.NotNull(landmarks.Spawn);

            Assert.True(DateTime.UtcNow < deadline, "The landmarks were not found in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static async Task WaitForTileAsync(TileRequest request)
    {
        var tileService = App.Services.GetRequiredService<IMapTileService>();
        var deadline = DateTime.UtcNow + _timeout;
        while (!tileService.TryGetTile(request, out _) || tileService.PendingCount > 0)
        {
            Assert.True(DateTime.UtcNow < deadline, "The map did not generate its tiles in time.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static BiomeColor ReadPixel(WriteableBitmap bitmap, int x, int y)
    {
        using var buffer = bitmap.Lock();
        var offset = y * buffer.RowBytes + x * BytesPerPixel;
        var first = Marshal.ReadByte(buffer.Address, offset);
        var green = Marshal.ReadByte(buffer.Address, offset + 1);
        var third = Marshal.ReadByte(buffer.Address, offset + 2);

        return buffer.Format == PixelFormat.Bgra8888
            ? new BiomeColor(third, green, first)
            : new BiomeColor(first, green, third);
    }
}
