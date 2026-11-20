using System.Globalization;
using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Exports a layer as PNG images, one row of map tiles at a time. The next row of tiles is generated while the current one
/// is coloured and written, and the work stays off the caller's thread wherever the runtime has other threads.
/// </summary>
public sealed class MapExportService : IMapExportService
{
    /// <summary>Wider or higher images open in few programs; tiles open everywhere.</summary>
    public const int WidelyReadableSide = 32_768;
    public const long LargePixelCount = 1L << 30;
    public const long LargeMemoryBytes = 1L << 30;
    public const int MinTileSize = 16;
    public const int MaxTileSize = 4096;

    private const int JobsPerWorker = 2;
    private const int RgbBytes = 3;
    private const int BgraBytes = 4;
    private const int ViewerBytesPerPixel = 4;
    private const int BiomeBytesPerSample = 1;
    private const int OneBytePaintedBytesPerSample = sizeof(short) + 1 + BgraBytes;
    private const int ColourBytesPerSample = sizeof(short) + RgbBytes + BgraBytes;
    private const int ShadedBiomeBytesPerSample = BiomeBytesPerSample + ColourBytesPerSample;
    private const double BytesPerGigabyte = 1L << 30;

    /// <summary>The most biomes a compact palette packs, next to the entry of no biome, in four bits per pixel.</summary>
    private const int MaxPackedBiomes = 15;

    /// <summary>The rows of tiles held at once: the one being written and the next, which is generated meanwhile.</summary>
    private const int TileRowsInMemory = 2;

    private static readonly JobPriority _priority = new(JobPriorityClass.Background);

    private readonly IComputeService _compute;
    private readonly ITileColorizer _colorizer;
    private readonly ILayerCatalog _layerCatalog;

    public MapExportService(IComputeService compute, ITileColorizer colorizer, ILayerCatalog layerCatalog)
    {
        _compute = compute;
        _colorizer = colorizer;
        _layerCatalog = layerCatalog;
    }

    private int MaxRunning => Math.Max(1, _compute.WorkerCount * JobsPerWorker);

    /// <summary>
    /// Where the bands of the PNG images are compressed: on the thread pool, or in the browser, whose one thread has no pool
    /// to share the work with, on the Web Workers of the compute service.
    /// </summary>
    private IPngBandCompressor Compressor => OperatingSystem.IsBrowser() ? new ComputePngBandCompressor(_compute, _priority) : ThreadPoolPngBandCompressor.Instance;

    public IReadOnlyList<int> GetScales(WorldKey world, LayerMode layer) => TileMetrics.For(world, layer).Scales;

    public IReadOnlyList<string> FindProblems(MapExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Worlds.Count == 0)
            return ["Choose the seeds to export."];

        var world = request.Worlds[0];
        if (!_layerCatalog.IsAvailable(request.Layer, world.Version, world.Dimension))
            return ["This layer does not exist in this version and dimension."];

        var scales = GetScales(world, request.Layer);
        if (!scales.Contains(request.Scale))
            return [$"This layer can be exported at {string.Join(", ", scales.Select(DescribeScale))} only."];
        if (request.Area.SizeX <= 0 || request.Area.SizeZ <= 0)
            return ["The area is empty."];
        if (Math.Max(Math.Abs((long)request.Area.X), Math.Abs((long)request.Area.Right)) > TilePyramid.WorldLimitBlocks
            || Math.Max(Math.Abs((long)request.Area.Z), Math.Abs((long)request.Area.Bottom)) > TilePyramid.WorldLimitBlocks)
            return ["The area has to lie within the 30 million blocks around the origin."];

        return FindSizeProblems(request);
    }

    public IReadOnlyList<string> FindWarnings(MapExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (FindProblems(request).Count > 0)
            return [];

        var layout = request.GetLayout();
        List<string> warnings = [];
        if (request.TileSize is null && (layout.Width > WidelyReadableSide || layout.Height > WidelyReadableSide))
            warnings.Add(Format($"Many programs cannot open images wider or higher than {WidelyReadableSide:N0} pixels; cut into tiles, the export opens everywhere."));
        if (layout.PixelCount > LargePixelCount)
            warnings.Add(Format($"With {layout.PixelCount:N0} pixels per seed, the export takes long, and opening the image needs about {layout.PixelCount * ViewerBytesPerPixel / BytesPerGigabyte:N1} GB of memory."));

        var world = request.Worlds[0];
        var isShaded = request.Layer.HasBiomeSamples && HasRelief(request, world);
        if (isShaded)
            warnings.Add("Shaded by terrain height, the image is written in colour, so the biome ids cannot be read back from it.");

        var format = Plan(request, world).Format;
        if (request.Layer.HasBiomeSamples && format.BitDepth < PngFormat.FullBitDepth)
            warnings.Add(Format($"With no more than {format.Palette.Count - 1} biomes in this layer, the image packs {PngFormat.FullBitDepth / format.BitDepth} pixels into a byte; its palette lists just those biomes, so a palette index is not the biome id."));

        var samples = TileMetrics.For(world, request.Layer).TileSamples;
        var bytesPerSample = format.ColorType switch
        {
            PngColorType.Indexed when request.Layer.HasBiomeSamples => BiomeBytesPerSample,
            PngColorType.Indexed or PngColorType.Grayscale => OneBytePaintedBytesPerSample,
            _ => isShaded ? ShadedBiomeBytesPerSample : ColourBytesPerSample,
        };
        var memory = (long)layout.Width * samples * TileRowsInMemory * bytesPerSample;
        if (memory > LargeMemoryBytes)
            warnings.Add(Format($"While it runs, the export holds about {memory / BytesPerGigabyte:N1} GB of memory for the rows of tiles it generates and writes."));

        return warnings;
    }

    public async Task ExportAsync(MapExportRequest request, Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (FindProblems(request) is [var problem, ..])
            throw new ArgumentException(problem, nameof(request));

        if (OperatingSystem.IsBrowser())
            await WriteAsync(request, destination, progress, cancellationToken).ConfigureAwait(false);
        else
            await Task.Run(() => WriteAsync(request, destination, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The name an image of a world gets inside an archive, or the prefix of the names of its tiles.</summary>
    public static string GetEntryName(WorldKey world, int? tileSize)
    {
        var seed = world.Seed.ToString(CultureInfo.InvariantCulture);
        return tileSize is null ? $"{seed}.png" : $"{seed}_";
    }

    private static IReadOnlyList<string> FindSizeProblems(MapExportRequest request)
    {
        if (request.TileSize is { } tileSize && (tileSize < MinTileSize || tileSize > MaxTileSize))
            return [Format($"Tiles can be from {MinTileSize} to {MaxTileSize:N0} pixels wide.")];
        return [];
    }

    private static string DescribeScale(int scale) => Format($"1:{scale}");

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private async Task WriteAsync(MapExportRequest request, Stream destination, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var layout = request.GetLayout();
        var rows = new RowProgress(progress, (long)layout.Height * request.Worlds.Count);
        var compressor = Compressor;
        using var buffer = new DeferredWriteStream();

        if (!request.IsArchive)
        {
            var plan = Plan(request, request.Worlds[0]);
            using var sink = WholeImageSink.Create(buffer, layout, plan.Format, compressor);
            await RenderAsync(request, request.Worlds[0], plan, layout, sink, buffer, destination, rows, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var world in request.Worlds)
                {
                    var plan = Plan(request, world);
                    using var sink = CreateArchiveSink(archive, request, world, plan.Format, layout, compressor);
                    await RenderAsync(request, world, plan, layout, sink, buffer, destination, rows, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await buffer.DrainToAsync(destination, cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IImageRowSink CreateArchiveSink(ZipArchive archive, MapExportRequest request, WorldKey world, PngFormat format, MapImageLayout layout, IPngBandCompressor compressor)
    {
        var name = GetEntryName(world, request.TileSize);
        return request.TileSize is { } tileSize
            ? new TiledImageSink(archive, name, layout, tileSize, format, compressor)
            : WholeImageSink.Create(archive, name, layout, format, compressor);
    }

    /// <summary>
    /// How an image of a world is stored and how the samples of its tiles become the bytes of its rows. Layers of biomes keep
    /// one palette index per biome id, with the id of no biome transparent, unless so few biomes can turn up that a palette of
    /// just those packs several pixels into a byte. Biomes shaded by the terrain and the other layers are painted: as indices
    /// of the steps of their ramp, in one byte of grey where the layer is painted in greys only, or in red, green and blue.
    /// </summary>
    private ImagePlan Plan(MapExportRequest request, WorldKey world)
    {
        var style = request.Style;
        switch (GetColorType(request, world))
        {
            case PngColorType.Indexed when request.Layer.HasBiomeSamples:
                return PlanBiomes(request, world);
            case PngColorType.Indexed:
                var palette = _colorizer.GetPalette(GetAnyTile(request, world), style)
                    ?? throw new InvalidOperationException($"The layer '{request.Layer}' has no palette in this style.");
                var indices = CreateIndexLookup(palette);
                return new ImagePlan(PngFormat.Indexed(palette, isSmooth: true), (row, tile) => ToIndices(Paint(row, tile, style), indices));
            case PngColorType.Grayscale:
                return new ImagePlan(PngFormat.Grayscale, (row, tile) => ToGray(Paint(row, tile, style)));
            default:
                return new ImagePlan(PngFormat.Rgb, (row, tile) => ToRgb(Paint(row, tile, style)));
        }
    }

    /// <summary>A compact palette lists the biomes that can turn up in ascending order of id, and no biome after them.</summary>
    private ImagePlan PlanBiomes(MapExportRequest request, WorldKey world)
    {
        var colors = _colorizer.GetBiomeColors(request.Style, world.Version);
        var biomes = _layerCatalog.GetBiomes(request.Layer, world.Version, world.Dimension);
        if (biomes.Count > MaxPackedBiomes)
            return new ImagePlan(PngFormat.Indexed(colors, Constants.NoBiomeTileValue), static (_, tile) => tile.Biomes);

        var compact = Enumerable.Repeat((byte)biomes.Count, Constants.NoBiomeTileValue + 1).ToArray();
        for (var index = 0; index < biomes.Count; index++)
            compact[biomes[index]] = (byte)index;
        RgbColor[] palette = [.. biomes.Select(biome => colors[biome]), colors[Constants.NoBiomeTileValue]];
        return new ImagePlan(PngFormat.Indexed(palette, transparentIndex: biomes.Count, isPacked: true), (_, tile) => Remap(tile.Biomes, compact));
    }

    private PngColorType GetColorType(MapExportRequest request, WorldKey world)
    {
        if (request.Layer.HasBiomeSamples)
            return HasRelief(request, world) ? PngColorType.Rgb : PngColorType.Indexed;

        var tile = GetAnyTile(request, world);
        if (_colorizer.PaintsGray(tile, request.Style))
            return PngColorType.Grayscale;
        return _colorizer.GetPalette(tile, request.Style) is null ? PngColorType.Rgb : PngColorType.Indexed;
    }

    /// <summary>Whether the style shades the layer of the world at the scale of the export; that depends on neither tile nor position.</summary>
    private bool HasRelief(MapExportRequest request, WorldKey world) => _colorizer.GetReliefTile(GetAnyTile(request, world), request.Style) is not null;

    /// <summary>A tile of the export's layer and scale, for what depends on neither tile nor position.</summary>
    private static TileRequest GetAnyTile(MapExportRequest request, WorldKey world)
        => MapContent.For(world, request.Layer, request.SampleHeight, request.Octaves).GetRequest(new TileKey(request.Scale, 0, 0));

    /// <summary>
    /// Generates the image one row of tiles at a time and passes its rows on as they are complete; the next row of tiles is
    /// generated meanwhile, so the workers do not wait for the image to be coloured, compressed and stored.
    /// </summary>
    private async Task RenderAsync(MapExportRequest request, WorldKey world, ImagePlan plan, MapImageLayout layout, IImageRowSink sink, DeferredWriteStream buffer,
        Stream destination, RowProgress rows, CancellationToken cancellationToken)
    {
        var tiles = new TileRowSource(MapContent.For(world, request.Layer, request.SampleHeight, request.Octaves), request.Scale, TileMetrics.For(world, request.Layer).TileSamples, layout);
        var bytesPerPixel = plan.Format.BytesPerPixel;
        var row = new byte[layout.Width * bytesPerPixel];
        var heights = new HeightRows((requests, token) => GenerateHeightsAsync(world, requests, token));

        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var next = GenerateAsync(tiles, tiles.FirstRow, request.Style, heights, stopping.Token);
        try
        {
            for (var tileZ = tiles.FirstRow; tileZ <= tiles.LastRow; tileZ++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var generated = await next.ConfigureAwait(false);
                if (tileZ < tiles.LastRow)
                    next = GenerateAsync(tiles, tileZ + 1, request.Style, heights, stopping.Token);

                var pixels = GetPixels(generated, plan);
                var top = Math.Max(tileZ * tiles.Samples, layout.SampleZ);
                var bottom = Math.Min((tileZ + 1) * tiles.Samples, layout.SampleZ + layout.Height);
                for (var sampleZ = top; sampleZ < bottom; sampleZ++)
                {
                    FillRow(row, pixels, tiles.FirstColumn, tiles.Samples, sampleZ - tileZ * tiles.Samples, layout, bytesPerPixel);
                    await sink.WriteRowAsync(row).ConfigureAwait(false);
                }

                rows.Add(bottom - top);
                await buffer.DrainToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            stopping.Cancel();
            next.Forget();
        }

        await sink.FinishAsync().ConfigureAwait(false);
    }

    /// <summary>Generates a row of map tiles together with the height tiles that shade them, if the style shades them.</summary>
    private async Task<TileRow> GenerateAsync(TileRowSource tiles, int tileZ, TileStyle style, HeightRows heights, CancellationToken cancellationToken)
    {
        TileRequest[] requests = [.. Enumerable.Range(tiles.FirstColumn, tiles.LastColumn - tiles.FirstColumn + 1).Select(tileX => tiles.Content.GetRequest(new TileKey(tiles.Scale, tileX, tileZ)))];
        var mapTiles = GenerateTilesAsync(requests, tiles.Samples, cancellationToken);
        var heightTiles = heights.GetAsync([.. requests.Select(request => _colorizer.GetReliefTile(request, style)).OfType<TileRequest>().Distinct()], cancellationToken);

        await Task.WhenAll(mapTiles, heightTiles).ConfigureAwait(false);
        return new TileRow(await mapTiles.ConfigureAwait(false), (await heightTiles.ConfigureAwait(false)).ToDictionary(tile => tile.Request));
    }

    private Task<TileData[]> GenerateHeightsAsync(WorldKey world, IReadOnlyList<TileRequest> requests, CancellationToken cancellationToken)
        => GenerateTilesAsync(requests, TileMetrics.For(world, LayerMode.Height).TileSamples, cancellationToken);

    private Task<TileData[]> GenerateTilesAsync(IEnumerable<TileRequest> requests, int samples, CancellationToken cancellationToken)
    {
        ComputeJob<TileData>[] jobs = [.. requests.Select(request => new TileJob(request, samples))];
        return _compute.RunAllAsync(jobs, _priority, MaxRunning, cancellationToken: cancellationToken);
    }

    /// <summary>The pixels of a row of tiles, made side by side where the runtime has threads to share the work.</summary>
    private static byte[][] GetPixels(TileRow row, ImagePlan plan)
    {
        var pixels = new byte[row.Tiles.Length][];
        if (OperatingSystem.IsBrowser())
        {
            for (var index = 0; index < row.Tiles.Length; index++)
                pixels[index] = plan.Paint(row, row.Tiles[index]);
        }
        else
        {
            Parallel.For(0, row.Tiles.Length, index => pixels[index] = plan.Paint(row, row.Tiles[index]));
        }

        return pixels;
    }

    /// <summary>The colours a tile is painted in, four bytes of blue, green, red and alpha per sample.</summary>
    private byte[] Paint(TileRow row, TileData tile, TileStyle style)
    {
        var bgra = new byte[tile.Samples * tile.Samples * BgraBytes];
        _colorizer.Colorize(tile, style, bgra, _colorizer.GetReliefTile(tile.Request, style) is { } relief ? row.Heights[relief] : null);
        return bgra;
    }

    private static byte[] ToRgb(byte[] bgra)
    {
        var count = bgra.Length / BgraBytes;
        var rgb = new byte[count * RgbBytes];
        for (var index = 0; index < count; index++)
        {
            rgb[index * RgbBytes] = bgra[index * BgraBytes + 2];
            rgb[index * RgbBytes + 1] = bgra[index * BgraBytes + 1];
            rgb[index * RgbBytes + 2] = bgra[index * BgraBytes];
        }

        return rgb;
    }

    private static byte[] ToGray(byte[] bgra)
    {
        var gray = new byte[bgra.Length / BgraBytes];
        for (var index = 0; index < gray.Length; index++)
            gray[index] = bgra[index * BgraBytes];
        return gray;
    }

    /// <summary>The palette index of each colour; a colour that appears twice in the palette takes its first entry.</summary>
    private static Dictionary<int, byte> CreateIndexLookup(IReadOnlyList<RgbColor> palette)
    {
        var indices = new Dictionary<int, byte>(palette.Count);
        for (var index = 0; index < palette.Count; index++)
            indices.TryAdd(palette[index].Red << 16 | palette[index].Green << 8 | palette[index].Blue, (byte)index);
        return indices;
    }

    private static byte[] ToIndices(byte[] bgra, Dictionary<int, byte> indices)
    {
        var pixels = new byte[bgra.Length / BgraBytes];
        for (var index = 0; index < pixels.Length; index++)
        {
            var color = bgra[index * BgraBytes + 2] << 16 | bgra[index * BgraBytes + 1] << 8 | bgra[index * BgraBytes];
            pixels[index] = indices.TryGetValue(color, out var paletteIndex)
                ? paletteIndex
                : throw new InvalidOperationException($"The layer painted the colour #{color:x6}, which its palette does not hold.");
        }

        return pixels;
    }

    private static byte[] Remap(byte[] biomes, byte[] compact)
    {
        var pixels = new byte[biomes.Length];
        for (var index = 0; index < biomes.Length; index++)
            pixels[index] = compact[biomes[index]];
        return pixels;
    }

    private static void FillRow(byte[] row, byte[][] tiles, int firstColumn, int samples, int tileRow, MapImageLayout layout, int bytesPerPixel)
    {
        for (var index = 0; index < tiles.Length; index++)
        {
            var tileLeft = (firstColumn + index) * samples;
            var left = Math.Max(tileLeft, layout.SampleX);
            var right = Math.Min(tileLeft + samples, layout.SampleX + layout.Width);
            var source = tiles[index].AsSpan((tileRow * samples + left - tileLeft) * bytesPerPixel, (right - left) * bytesPerPixel);
            source.CopyTo(row.AsSpan((left - layout.SampleX) * bytesPerPixel));
        }
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    /// <summary>How an image is stored, and how the samples of a tile of a row of tiles become the bytes of its rows.</summary>
    private sealed record ImagePlan(PngFormat Format, Func<TileRow, TileData, byte[]> Paint);

    /// <summary>The map tiles an image is made of: what they show, at which scale, and the columns and rows of them it spans.</summary>
    private sealed record TileRowSource(MapContent Content, int Scale, int Samples, MapImageLayout Layout)
    {
        public int FirstColumn => FloorDivide(Layout.SampleX, Samples);
        public int LastColumn => FloorDivide(Layout.SampleX + Layout.Width - 1, Samples);
        public int FirstRow => FloorDivide(Layout.SampleZ, Samples);
        public int LastRow => FloorDivide(Layout.SampleZ + Layout.Height - 1, Samples);
    }

    /// <summary>A row of generated map tiles and the height tiles that shade them, by their requests.</summary>
    private sealed record TileRow(TileData[] Tiles, IReadOnlyDictionary<TileRequest, TileData> Heights);

    /// <summary>
    /// The height tiles of the row of map tiles generated last. One height tile can lie under several rows of map tiles,
    /// so the next row reuses them when it needs the same ones.
    /// </summary>
    private sealed class HeightRows(Func<IReadOnlyList<TileRequest>, CancellationToken, Task<TileData[]>> generate)
    {
        private TileRequest[] _requests = [];
        private Task<TileData[]> _tiles = Task.FromResult<TileData[]>([]);

        public Task<TileData[]> GetAsync(TileRequest[] requests, CancellationToken cancellationToken)
        {
            if (!requests.SequenceEqual(_requests))
                (_requests, _tiles) = (requests, generate(requests, cancellationToken));
            return _tiles;
        }
    }

    /// <summary>Reports the share of all rows of all images written so far.</summary>
    private sealed class RowProgress(IProgress<double>? progress, long total)
    {
        private long _done;

        public void Add(int rows)
        {
            _done += rows;
            progress?.Report(total == 0 ? 1 : _done / (double)total);
        }
    }
}
