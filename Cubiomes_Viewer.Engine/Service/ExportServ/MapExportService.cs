using System.Globalization;
using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

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
    private const int ColourBytesPerSample = sizeof(short) + RgbBytes + BgraBytes;
    private const double BytesPerGigabyte = 1L << 30;

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

        var samples = TileMetrics.For(request.Worlds[0], request.Layer).TileSamples;
        var memory = (long)layout.Width * samples * (request.Layer.HasBiomeSamples ? BiomeBytesPerSample : ColourBytesPerSample);
        if (memory > LargeMemoryBytes)
            warnings.Add(Format($"While it runs, the export holds about {memory / BytesPerGigabyte:N1} GB of memory for each band of rows it writes."));

        return warnings;
    }

    public async Task ExportAsync(MapExportRequest request, Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (FindProblems(request) is [var problem, ..])
            throw new ArgumentException(problem, nameof(request));

        var layout = request.GetLayout();
        var rows = new RowProgress(progress, (long)layout.Height * request.Worlds.Count);
        using var buffer = new DeferredWriteStream();

        if (!request.IsArchive)
        {
            using var sink = WholeImageSink.Create(buffer, layout, GetFormat(request, request.Worlds[0]));
            await RenderAsync(request, request.Worlds[0], layout, sink, buffer, destination, rows, cancellationToken);
        }
        else
        {
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var world in request.Worlds)
                {
                    using var sink = CreateArchiveSink(archive, request, world, layout);
                    await RenderAsync(request, world, layout, sink, buffer, destination, rows, cancellationToken);
                }
            }
        }

        await buffer.DrainToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
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

    private IImageRowSink CreateArchiveSink(ZipArchive archive, MapExportRequest request, WorldKey world, MapImageLayout layout)
    {
        var format = GetFormat(request, world);
        var name = GetEntryName(world, request.TileSize);
        return request.TileSize is { } tileSize
            ? new TiledImageSink(archive, name, layout, tileSize, format)
            : WholeImageSink.Create(archive, name, layout, format);
    }

    /// <summary>Layers of biomes keep one palette index per biome id, with the id of no biome transparent; other layers are painted.</summary>
    private PngFormat GetFormat(MapExportRequest request, WorldKey world)
        => request.Layer.HasBiomeSamples
            ? PngFormat.Indexed(_colorizer.GetBiomeColors(request.Style, world.Version), Constants.NoBiomeTileValue)
            : PngFormat.Rgb;

    /// <summary>Generates the image one row of tiles at a time and passes the rows on as they are complete.</summary>
    private async Task RenderAsync(MapExportRequest request, WorldKey world, MapImageLayout layout, IImageRowSink sink, DeferredWriteStream buffer, Stream destination, RowProgress rows, CancellationToken cancellationToken)
    {
        var content = MapContent.For(world, request.Layer, request.SampleHeight, request.Octaves);
        var samples = TileMetrics.For(world, request.Layer).TileSamples;
        var bytesPerPixel = request.Layer.HasBiomeSamples ? 1 : RgbBytes;
        var firstColumn = FloorDivide(layout.SampleX, samples);
        var lastColumn = FloorDivide(layout.SampleX + layout.Width - 1, samples);
        var row = new byte[layout.Width * bytesPerPixel];

        for (var tileZ = FloorDivide(layout.SampleZ, samples); tileZ <= FloorDivide(layout.SampleZ + layout.Height - 1, samples); tileZ++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ComputeJob<TileData>[] jobs = [.. Enumerable.Range(firstColumn, lastColumn - firstColumn + 1).Select(tileX => new TileJob(content.GetRequest(new TileKey(request.Scale, tileX, tileZ)), samples))];
            var tiles = await _compute.RunAllAsync(jobs, _priority, MaxRunning, cancellationToken: cancellationToken);
            var pixels = Array.ConvertAll(tiles, tile => GetPixels(tile, request.Style));

            var top = Math.Max(tileZ * samples, layout.SampleZ);
            var bottom = Math.Min((tileZ + 1) * samples, layout.SampleZ + layout.Height);
            for (var sampleZ = top; sampleZ < bottom; sampleZ++)
            {
                FillRow(row, pixels, firstColumn, samples, sampleZ - tileZ * samples, layout, bytesPerPixel);
                sink.WriteRow(row);
            }

            rows.Add(bottom - top);
            await buffer.DrainToAsync(destination, cancellationToken);
        }

        sink.Finish();
    }

    /// <summary>The bytes of a tile's pixels, row by row: biome ids, or red, green and blue.</summary>
    private byte[] GetPixels(TileData tile, TileStyle style)
    {
        if (!tile.HasValues)
            return tile.Biomes;

        var count = tile.Samples * tile.Samples;
        var bgra = new byte[count * BgraBytes];
        _colorizer.Colorize(tile, style, bgra);

        var rgb = new byte[count * RgbBytes];
        for (var index = 0; index < count; index++)
        {
            rgb[index * RgbBytes] = bgra[index * BgraBytes + 2];
            rgb[index * RgbBytes + 1] = bgra[index * BgraBytes + 1];
            rgb[index * RgbBytes + 2] = bgra[index * BgraBytes];
        }

        return rgb;
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
