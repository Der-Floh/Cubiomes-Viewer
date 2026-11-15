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
    private static readonly TileStyle _relief = new(BiomeRelief: true);

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
        { "biomes 1.21.3", McVersion.V1_21_3, Dimension.Overworld, LayerMode.Biomes, 4, HeightStyle.Shaded, null, "39ac7531f4224dc63464589d7c5e380372f506eaf69456d5dc98245bb34d24ae" },
        { "biomes 1.12.2", McVersion.V1_12_2, Dimension.Overworld, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "7d2c102daf2ae68d29ea5b365aa4b09b29d3e179e2dd3383e2e79a672b86c563" },
        { "biomes Beta 1.7", McVersion.Beta1_7, Dimension.Overworld, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "9f4f1673ef22a6b5a7bbc61de11f556305f3b3c65db6afe04c371b00505ad116" },
        { "nether 1.16.5", McVersion.V1_16_5, Dimension.Nether, LayerMode.Biomes, 4, HeightStyle.Shaded, null, "7bb4f3edf84f0e300a1785ff595b827275782ea3e0b32e8682975fa87064fb1b" },
        { "end 1.21.3", McVersion.V1_21_3, Dimension.End, LayerMode.Biomes, 16, HeightStyle.Shaded, null, "1d55b799be5cc4ad9054332fe9934ee6e02a5e37b27ba64c22c8949fe21901e2" },
        { "village potential 1.20.6", McVersion.V1_20_6, Dimension.Overworld, LayerMode.StructurePotential, 16, HeightStyle.Shaded, StructureType.Village, "21dc46a1aa2034d64528fe6d651b2ea3ca158df835c40aff4466860f2f891a3a" },
        { "ocean temperature 1.16.5", McVersion.V1_16_5, Dimension.Overworld, LayerMode.OceanTemperature, 16, HeightStyle.Shaded, null, "968c8c0d70e88a5d3c86b189a37e8886121483f457e43cafc9397928891ad30e" },
        { "temperature 1.18.2", McVersion.V1_18_2, Dimension.Overworld, LayerMode.Temperature, 16, HeightStyle.Shaded, null, "5a6c409acfa175fec86c9d9733e90e020de3338661f839dc3883960367c25978" },
        { "height shaded 1.21.3", McVersion.V1_21_3, Dimension.Overworld, LayerMode.Height, 16, HeightStyle.Shaded, null, "4e127b30ebd273fb62b88f13b0f28bb234285bd3fd4ad8507777bd089e4d5f29" },
        { "height contours 1.12.2", McVersion.V1_12_2, Dimension.Overworld, LayerMode.Height, 16, HeightStyle.Contours, null, "edb97f0414abab05126f0500a078248eb9a6495b334f379f72d16148337d421b" },
    };

    [Theory]
    [MemberData(nameof(FixedExports))]
    public async Task Export_OfAFixedWorld_ShowsWhatItShowedWhenItWasRecorded(string name, McVersion version, Dimension dimension, LayerMode layer, int scale, HeightStyle height, StructureType? potential, string expectedHash)
    {
        var request = Request(World(262, version, dimension), layer, scale) with { Style = new TileStyle(height, potential) };

        var image = PngReader.Read(await ExportAsync(request));

        Assert.True(expectedHash == image.ContentHash, $"{name}: {image.ContentHash}");
    }

    public static TheoryData<string, McVersion, Dimension, int, string> FixedShadedExports() => new()
    {
        { "shaded biomes 1.21.3", McVersion.V1_21_3, Dimension.Overworld, 4, "e41c5a2b6375c4fc3a3d16812b05f1d4e1e6669bbe4176aeaec95fc27f2a3095" },
        { "shaded biomes 1.21.3 at 1:1", McVersion.V1_21_3, Dimension.Overworld, 1, "02a16837ee3cbd8007b1398cec7ed76f3280828cd5e3317d0290fd9c989edfc9" },
        { "shaded biomes 1.12.2", McVersion.V1_12_2, Dimension.Overworld, 16, "967d4b6622714c2bbe552b827056b72ca5e06d113cee94ac426a082989b72915" },
        { "shaded end 1.21.3", McVersion.V1_21_3, Dimension.End, 16, "25dfa22471bd2c260bc9390ce08b6b95e5cacfdd3f095fa14957489198ac0a4c" },
    };

    [Theory]
    [MemberData(nameof(FixedShadedExports))]
    public async Task ShadedExport_OfAFixedWorld_ShowsWhatItShowedWhenItWasRecorded(string name, McVersion version, Dimension dimension, int scale, string expectedHash)
    {
        var request = Request(World(262, version, dimension), LayerMode.Biomes, scale) with { Style = _relief };

        var image = PngReader.Read(await ExportAsync(request));

        Assert.True(expectedHash == image.ContentHash, $"{name}: {image.ContentHash}");
    }

    [Fact]
    public async Task Biomes_ShadedByTerrain_AreWrittenInColoursThatDifferFromTheFlatOnes()
    {
        var request = Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 4);

        var flat = PngReader.Read(await ExportAsync(request));
        var shaded = PngReader.Read(await ExportAsync(request with { Style = _relief }));

        Assert.Equal((flat.Width, flat.Height, PngColorType.Rgb), (shaded.Width, shaded.Height, shaded.ColorType));
        var changed = Enumerable.Range(0, flat.Pixels.Length).Count(pixel => !flat.Palette.AsSpan(flat.Pixels[pixel] * 3, 3).SequenceEqual(shaded.Pixels.AsSpan(pixel * 3, 3)));
        Assert.True(changed > flat.Pixels.Length / 10, $"Relief changed {changed} of {flat.Pixels.Length} pixels.");
    }

    [Theory]
    [InlineData(McVersion.V1_12_2, Dimension.Overworld, 256)]
    [InlineData(McVersion.V1_16_5, Dimension.Nether, 4)]
    public async Task Biomes_ShadedWhereNoHeightsReach_StayPalettePixelsOfTheGeneratedBiomes(McVersion version, Dimension dimension, int scale)
    {
        var request = Request(World(262, version, dimension), LayerMode.Biomes, scale) with { Style = _relief };

        var image = PngReader.Read(await ExportAsync(request));

        Assert.Equal(PngColorType.Indexed, image.ColorType);
        Assert.Equal(GenerateBiomes(request.Worlds[0], request.GetLayout(), scale), image.Pixels);
        Assert.Empty(_export.FindWarnings(request));
    }

    [Fact]
    public void FindWarnings_ShadedBiomes_SayTheBiomeIdsCannotBeReadBack()
    {
        var request = Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 4) with { Style = _relief };

        Assert.Contains("biome ids cannot be read back", Assert.Single(_export.FindWarnings(request)), StringComparison.Ordinal);
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
    [InlineData(McVersion.V1_21_3, 1, 0, 40_000, null, null)]
    [InlineData(McVersion.V1_21_3, 1, 0, 40_000, 512, null)]
    [InlineData(McVersion.V1_21_3, 4, 0, 2000, 8, "Tiles can be from 16 to 4,096 pixels wide.")]
    [InlineData(McVersion.V1_21_3, 1, 29_999_000, 2000, null, "The area has to lie within the 30 million blocks around the origin.")]
    public void FindProblems_Request_NamesWhatIsWrong(McVersion version, int scale, int x, int width, int? tileSize, string? expected)
    {
        var request = new MapExportRequest { Worlds = [World(1, version, Dimension.Overworld)], Area = new BlockArea(x, 0, width, 2000), Scale = scale, TileSize = tileSize };

        Assert.Equal(expected is null ? [] : [expected], _export.FindProblems(request));
    }

    [Fact]
    public void FindWarnings_ImageWiderThanMostProgramsOpen_WarnsButLetsItBeMade()
    {
        var request = new MapExportRequest { Worlds = [World(1, McVersion.V1_21_3, Dimension.Overworld)], Area = new BlockArea(0, 0, 40_000, 2000), Scale = 1 };

        Assert.Empty(_export.FindProblems(request));
        Assert.Contains("cannot open images wider or higher than 32,768 pixels", Assert.Single(_export.FindWarnings(request)), StringComparison.Ordinal);
        Assert.Empty(_export.FindWarnings(request with { TileSize = 512 }));
    }

    [Fact]
    public void FindWarnings_HugeExport_NamesItsPixelsAndTheMemoryItNeeds()
    {
        var request = new MapExportRequest { Worlds = [World(1, McVersion.V1_21_3, Dimension.Overworld)], Area = new BlockArea(-15_000_000, 0, 30_000_000, 1000), Scale = 1, TileSize = 4096 };

        var warnings = _export.FindWarnings(request);

        Assert.Empty(_export.FindProblems(request));
        Assert.Equal(2, warnings.Count);
        Assert.Contains("30,000,000,000 pixels per seed", warnings[0], StringComparison.Ordinal);
        Assert.Contains("GB of memory for the rows of tiles", warnings[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportAsync_StartedOnAThreadWithASynchronizationContext_DoesItsWorkElsewhere()
    {
        var context = new CountingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        using var stream = new MemoryStream();
        Task export;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            export = _export.ExportAsync(Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Height, 16), stream, cancellationToken: TestContext.Current.CancellationToken);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await export;

        Assert.True(stream.Length > 0);
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public void FindWarnings_ExportThatOpensEverywhere_HasNone()
        => Assert.Empty(_export.FindWarnings(Request(World(262, McVersion.V1_21_3, Dimension.Overworld), LayerMode.Biomes, 4)));

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

    /// <summary>Counts the work posted to it, which a user interface thread would have to do, and does that work on the thread pool.</summary>
    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
}
