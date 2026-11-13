using System.Diagnostics.CodeAnalysis;

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

using Cubiomes_Viewer.Controls;
using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.TileBitmapServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Controls.Map;

/// <summary>While the tiles of a new height load, the map keeps showing the last height instead of going blank.</summary>
public sealed class TileLayerTests
{
    private const int ViewSize = 128;
    private const int LowHeight = 64;
    private const int HighHeight = 68;
    private const byte MushroomFields = 14;
    private const byte Desert = 2;

    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
    private static readonly RgbColor _background = new(0, 0, 0);

    [AvaloniaFact]
    public void Render_HeightChangedBeforeItsTilesArrive_ShowsThePreviousHeightUntilThen()
    {
        var tiles = new FixedTileService();
        tiles.Fill(_world.Seed, LowHeight, MushroomFields);
        var (window, map) = Show(tiles);

        Assert.Equal(GetColor(MushroomFields), RenderCentre(window, map));

        map.SampleHeight = HighHeight;
        Assert.Equal(GetColor(MushroomFields), RenderCentre(window, map));

        tiles.Fill(_world.Seed, HighHeight, Desert);
        Assert.Equal(GetColor(Desert), RenderCentre(window, map));
        window.Close();
    }

    [AvaloniaFact]
    public void Render_SeedChangedBeforeItsTilesArrive_ShowsNothingOfTheOldWorld()
    {
        var tiles = new FixedTileService();
        tiles.Fill(_world.Seed, LowHeight, MushroomFields);
        var (window, map) = Show(tiles);
        Assert.Equal(GetColor(MushroomFields), RenderCentre(window, map));

        map.World = _world with { Seed = _world.Seed + 1 };

        Assert.Equal(_background, RenderCentre(window, map));
        window.Close();
    }

    private static (Window Window, MapView Map) Show(IMapTileService tiles)
    {
        var layer = new TileLayer(tiles, App.Services.GetRequiredService<ITileColorizer>(), App.Services.GetRequiredService<ITileBitmapService>());
        var map = new MapView { World = _world, SampleHeight = LowHeight, Camera = new MapCamera(0, 0, 1), SmoothMotion = false, Background = Brushes.Black };
        map.AddLayer(layer);
        var window = new Window { Width = ViewSize, Height = ViewSize, Content = map };
        window.Show();
        return (window, map);
    }

    private static RgbColor RenderCentre(Window window, MapView map)
    {
        map.Refresh();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        return FramePixels.Read(frame, ViewSize / 2, ViewSize / 2);
    }

    private static RgbColor GetColor(byte biome) => App.Services.GetRequiredService<IBiomeCatalog>().DefaultPalette[biome];

    /// <summary>Has tiles of one biome for the heights of the seeds it was filled with, and none for any other.</summary>
    private sealed class FixedTileService : IMapTileService
    {
        private const int Samples = 4;

        private readonly Dictionary<(long Seed, int Y), byte> _biomes = [];

        public event Action? StateChanged
        {
            add { }
            remove { }
        }

        public int PendingCount => 0;

        public long CacheBudgetBytes { get; set; }

        public void Fill(long seed, int y, byte biome) => _biomes[(seed, y)] = biome;

        public bool TryGetTile(TileRequest request, [NotNullWhen(true)] out TileData? tile)
        {
            tile = _biomes.TryGetValue((request.World.Seed, request.Y), out var biome)
                ? new TileData { Request = request, Samples = Samples, Biomes = [.. Enumerable.Repeat(biome, Samples * Samples)] }
                : null;
            return tile is not null;
        }

        public void SetVisibleTiles(IReadOnlyList<TileRequest> requests)
        {
        }

        public bool TryGetCachedBiome(MapContent content, int blockX, int blockZ, out int biome)
        {
            biome = 0;
            return false;
        }

        public bool TryGetCachedValue(MapContent content, int blockX, int blockZ, out short value)
        {
            value = 0;
            return false;
        }
    }
}
