using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ExportServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.ExportServ;

/// <summary>Exported images against the generator, against each other cut differently, and against hashes recorded once.</summary>
public sealed class MapExportServiceTests : IDisposable
{
    private const int SampleHeight = 64;

    private static readonly BlockArea _area = new(-1000, -700, 2000, 1500);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;
    private readonly IMapExportService _export;

    public MapExportServiceTests()
    {
        _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(new FileSystemAppDataStore(_directory)).BuildServiceProvider();
        _export = _services.GetRequiredService<IMapExportService>();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    public static TheoryData<McVersion, Dimension, int> BiomeWorlds() => new()
    {
        { McVersion.V1_21_3, Dimension.Overworld, 4 },
        { McVersion.V1_18_2, Dimension.Overworld, 16 },
        { McVersion.V1_12_2, Dimension.Overworld, 16 },
        { McVersion.V1_12_2, Dimension.Overworld, 1 },
        { McVersion.V1_16_5, Dimension.Nether, 4 },
        { McVersion.V1_21_3, Dimension.End, 16 },
        { McVersion.Beta1_7, Dimension.Overworld, 16 },
        { McVersion.Beta1_7, Dimension.Overworld, 1 },
    };

    [Theory]
    [MemberData(nameof(BiomeWorlds))]
    public async Task Biomes_OfOneSeed_ArePalettePixelsOfTheGeneratedBiomes(McVersion version, Dimension dimension, int scale)
    {
        var request = Request(World(262, version, dimension), LayerMode.Biomes, scale);

        var image = PngReader.Read(await ExportAsync(request));

        var layout = request.GetLayout();
        Assert.Equal((layout.Width, layout.Height, PngColorType.Indexed), (image.Width, image.Height, image.ColorType));
        Assert.Equal(GenerateBiomes(request.Worlds[0], layout, scale), image.Pixels);
        var colors = _services.GetRequiredService<ITileColorizer>().GetBiomeColors(default, version);
        Assert.Equal(colors.SelectMany(color => new[] { color.Red, color.Green, color.Blue }), image.Palette);
        Assert.Equal(Constants.NoBiomeTileValue + 1, image.Transparency.Length);
        Assert.Equal(0, image.Transparency[^1]);
    }

    public static TheoryData<string, McVersion, Dimension, LayerMode, int, HeightStyle, StructureType?, string> FixedExports() => new()
    {
        { "biomes 1.21.3", McVersion.V1_21_3, Dimension.Overworld, LayerMode.Biomes, 4, HeightStyle.Shaded, null, "682012674c80850630978741900adc9c7a4842d215f289622ee1a46a67329b53" },
        { "biomes 1.12.2", McVersion.V1_12_2, Dimension.Overworld, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "fc95c96fadee2416860006d10b990e68ba84dae68d2924ddbd1e474874de1f8a" },
        { "biomes Beta 1.7", McVersion.Beta1_7, Dimension.Overworld, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "a9ddf40cc5e4404a4c2b1cd10bb413a083d37c79a6ed09d45ea67c628656d862" },
        { "nether 1.16.5", McVersion.V1_16_5, Dimension.Nether, LayerMode.Biomes, 4, HeightStyle.Shaded, null, "10ae64e818080402f7f2a67a75aa4531798a83044d5fb571c89950e5f63f7a52" },
        { "end 1.21.3", McVersion.V1_21_3, Dimension.End, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "c4894f4f7f6691ea6c73fb36bc245bd5ebfea4e04a5641fa9cd979534db4f6cb" },
        { "village potential 1.20.6", McVersion.V1_20_6, Dimension.Overworld, LayerMode.StructurePotential, 16, HeightStyle.Shaded, StructureType.Village, "fd0bc1f3b27e06d4b9d6bd6135f580afdcca876e2ed286ed06aa5eb49c6a3d3c" },
        { "ocean temperature 1.16.5", McVersion.V1_16_5, Dimension.Overworld, LayerMode.OceanTemperature, 16, HeightStyle.Shaded, null, "4757f263a3d3268cac6ca4d10b3ae1a3fee487a6b9316c756972e3b488ae6e56" },
        { "temperature 1.18.2", McVersion.V1_18_2, Dimension.Overworld, LayerMode.Temperature, 16, HeightStyle.Shaded, null, "5a6c409acfa175fec86c9d9733e90e020de3338661f839dc3883960367c25978" },
        { "height shaded 1.21.3", McVersion.V1_21_3, Dimension.Overworld, LayerMode.Height, 16, HeightStyle.Shaded, null, "a78e0b6a083b6bb1cde344027a400e3ab3c1322297db39d441a51a605bfae1b4" },
        { "height contours 1.12.2", McVersion.V1_12_2, Dimension.Overworld, LayerMode.Height, 16, HeightStyle.Contours, null, "d70e95fb3669103dde9eae4b47b77af977834485181834a5cc189c2dd115957f" },
    };

    [Theory]
    [MemberData(nameof(FixedExports))]
    public async Task Export_OfAFixedWorld_ShowsWhatItShowedWhenItWasRecorded(string name, McVersion version, Dimension dimension, LayerMode layer, int scale, HeightStyle height, StructureType? potential, string expectedHash)
    {
        var request = Request(World(262, version, dimension), layer, scale) with { Style = new TileStyle(height, potential) };

        var image = PngReader.Read(await ExportAsync(request));

        Assert.True(expectedHash == image.ContentHash, $"{name}: {image.ContentHash}");
    }

    [Fact]
    public async Task Tiles_FitTogetherIntoTheWholeImage()
    {
        var whole = Request(World(-77, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Height, 4);
        var tiled = whole with { TileSize = 100 };
        var layout = whole.GetLayout();

        var image = PngReader.Read(await ExportAsync(whole));
        var tiles = ReadArchive(await ExportAsync(tiled));

        var (columns, rows) = layout.CountTiles(100);
        Assert.Equal(columns * rows, tiles.Count);
        for (var z = 0; z < layout.Height; z++)
        {
            for (var column = 0; column < columns; column++)
            {
                var tile = tiles[TiledImageSink.GetEntryName("-77_", column, z / 100)];
                Assert.Equal((Math.Min(100, layout.Width - column * 100), Math.Min(100, layout.Height - z / 100 * 100)), (tile.Width, tile.Height));
                Assert.True(image.GetRow(z).Slice(column * 100 * 3, tile.Width * 3).SequenceEqual(tile.GetRow(z % 100)));
            }
        }
    }

    [Fact]
    public async Task SeveralSeeds_GiveOneImageEachNamedAfterItsSeed()
    {
        var first = World(1, McVersion.V1_16_5, Dimension.Overworld);
        var second = World(-5, McVersion.V1_16_5, Dimension.Overworld);
        var request = Request(first, LayerMode.Biomes, 16) with { Worlds = [first, second] };

        var images = ReadArchive(await ExportAsync(request));

        Assert.Equal(["-5.png", "1.png"], images.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(PngReader.Read(await ExportAsync(request with { Worlds = [second] })).Pixels, images["-5.png"].Pixels);
        Assert.NotEqual(images["1.png"].Pixels, images["-5.png"].Pixels);
    }

    [Fact]
    public async Task Destination_ThatTakesOnlyAsynchronousWrites_ReceivesTheImage()
    {
        var request = Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 16);
        using var destination = new AsynchronousOnlyStream();

        await _export.ExportAsync(request, destination, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PngReader.Read(await ExportAsync(request)).Pixels, PngReader.Read(destination.ToArray()).Pixels);
    }

    [Fact]
    public async Task Progress_RisesToTheWhole()
    {
        var request = Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 4);
        List<double> reported = [];

        await _export.ExportAsync(request, new MemoryStream(), new SynchronousProgress(reported.Add), TestContext.Current.CancellationToken);

        Assert.True(reported.Count > 1);
        Assert.Equal(reported.Order(), reported);
        Assert.Equal(1, reported[^1], 6);
    }

    [Fact]
    public async Task Export_Cancelled_Stops()
    {
        var request = Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 4);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _export.ExportAsync(request, new MemoryStream(), cancellationToken: new CancellationToken(canceled: true)));
    }

    [Theory]
    [InlineData(McVersion.V1_12_2, 1024, 0, 2000, null, "This layer can be exported at 1:1, 1:4, 1:16, 1:64, 1:256 only.")]
    [InlineData(McVersion.V1_21_3, 4, 0, 0, null, "The area is empty.")]
    [InlineData(McVersion.V1_21_3, 1, 0, 40_000, null, "An image can be at most 32,768 pixels wide and high; cut it into tiles or choose a coarser scale.")]
    [InlineData(McVersion.V1_21_3, 1, 0, 40_000, 512, null)]
    [InlineData(McVersion.V1_21_3, 4, 0, 2000, 8, "Tiles can be from 16 to 4,096 pixels wide.")]
    [InlineData(McVersion.V1_21_3, 1, 29_999_000, 2000, null, "The area has to lie within the 30 million blocks around the origin.")]
    public void FindProblems_Request_NamesWhatIsWrong(McVersion version, int scale, int x, int width, int? tileSize, string? expected)
    {
        var request = new MapExportRequest { Worlds = [World(1, version, Dimension.Overworld)], Area = new BlockArea(x, 0, width, 2000), Scale = scale, TileSize = tileSize };

        Assert.Equal(expected is null ? [] : [expected], _export.FindProblems(request));
    }

    [Fact]
    public void FindProblems_LayerTheWorldLacks_IsAProblem()
    {
        var request = new MapExportRequest { Worlds = [World(1, McVersion.V1_16_5, Dimension.Nether)], Layer = LayerMode.Height, Area = _area, Scale = 16 };

        Assert.Equal(["This layer does not exist in this version and dimension."], _export.FindProblems(request));
    }

    private static WorldKey World(long seed, McVersion version, Dimension dimension) => new(seed, version, dimension, GeneratorFlags.ForceOceanVariants);

    private static MapExportRequest Request(WorldKey world, LayerMode layer, int scale)
        => new() { Worlds = [world], Layer = layer, SampleHeight = SampleHeight, Area = _area, Scale = scale };

    private async Task<byte[]> ExportAsync(MapExportRequest request)
    {
        using var stream = new MemoryStream();
        await _export.ExportAsync(request, stream, cancellationToken: TestContext.Current.CancellationToken);
        return stream.ToArray();
    }

    private static Dictionary<string, PngImage> ReadArchive(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(entry => entry.FullName, entry =>
        {
            using var content = new MemoryStream();
            using (var stream = entry.Open())
                stream.CopyTo(content);
            return PngReader.Read(content.ToArray());
        });
    }

    private static byte[] GenerateBiomes(WorldKey world, MapImageLayout layout, int scale)
    {
        using var generator = new CubiomesWorldGenerator(world.Version, world.Flags);
        generator.Apply(world.Dimension, world.Seed);
        var biomes = new byte[layout.PixelCount];
        generator.GenerateBiomes(new BiomeArea(scale, layout.SampleX, layout.SampleZ, layout.Width, layout.Height, scale == 1 ? SampleHeight : SampleHeight >> 2), biomes);
        return biomes;
    }

    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    /// <summary>Like a file of the browser: writing without awaiting is not possible.</summary>
    private sealed class AsynchronousOnlyStream : Stream
    {
        private readonly MemoryStream _content = new();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _content.Length;

        public override long Position
        {
            get => _content.Position;
            set => throw new NotSupportedException();
        }

        public byte[] ToArray() => _content.ToArray();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Only asynchronous writes.");

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _content.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _content.Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _content.Dispose();
            base.Dispose(disposing);
        }
    }
}
