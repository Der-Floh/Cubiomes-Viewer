using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ExportServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.ExportServ;

public sealed class PngWriterTests
{
    [Fact]
    public async Task Image_EndsWithTheEndChunkEveryPngEndsWith()
    {
        byte[] endChunk = [0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82];

        var png = await WriteAsync(PngFormat.Rgb, 1, [[1, 2, 3]]);

        Assert.Equal(endChunk, png[^endChunk.Length..]);
    }

    [Theory]
    [InlineData(16, 16)]
    [InlineData(512, 512)]
    [InlineData(1, 100_000)]
    public void Band_OfASmallImage_HoldsNoMoreRowsThanTheImage(int width, int height)
    {
        using var writer = new PngWriter(new MemoryStream(), width, height, PngFormat.Rgb);

        Assert.InRange(writer.RowsPerBand, 1, height);
        Assert.True((long)writer.RowsPerBand * width * PngFormat.Rgb.BytesPerPixel <= Math.Max(4 << 20, width * PngFormat.Rgb.BytesPerPixel));
    }

    [Fact]
    public async Task RgbImage_ReadsBackRowByRow()
    {
        byte[][] rows = [[1, 2, 3, 250, 251, 252], [0, 0, 0, 255, 255, 255], [9, 8, 7, 1, 1, 1]];

        var image = PngReader.Read(await WriteAsync(PngFormat.Rgb, 2, rows));

        Assert.Equal((2, 3, PngColorType.Rgb), (image.Width, image.Height, image.ColorType));
        Assert.Equal(rows.SelectMany(row => row), image.Pixels);
    }

    [Fact]
    public async Task GrayscaleImage_ReadsBackRowByRow()
    {
        byte[][] rows = [[0, 40, 41, 255], [40, 40, 42, 254], [41, 0, 0, 1]];

        var image = PngReader.Read(await WriteAsync(PngFormat.Grayscale, 4, rows));

        Assert.Equal((4, 3, PngColorType.Grayscale), (image.Width, image.Height, image.ColorType));
        Assert.Equal(rows.SelectMany(row => row), image.Pixels);
    }

    [Fact]
    public async Task IndexedImage_KeepsItsPaletteAndTheTransparentEntry()
    {
        var format = PngFormat.Indexed([new RgbColor(10, 20, 30), new RgbColor(40, 50, 60), new RgbColor(70, 80, 90)], transparentIndex: 2);
        byte[][] rows = [[0, 1, 2, 1], [2, 2, 0, 0]];

        var image = PngReader.Read(await WriteAsync(format, 4, rows));

        Assert.Equal(PngColorType.Indexed, image.ColorType);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80, 90], image.Palette);
        Assert.Equal([255, 255, 0], image.Transparency);
        Assert.Equal(rows.SelectMany(row => row), image.Pixels);
    }

    [Fact]
    public async Task LargeImage_IsCutIntoSeveralDataChunksAndStillReadsBack()
    {
        var random = new Random(7);
        var rows = Enumerable.Range(0, 300).Select(_ => { var row = new byte[900]; random.NextBytes(row); return row; }).ToArray();

        var png = await WriteAsync(PngFormat.Rgb, 300, rows);

        Assert.True(png.Length > 3 * 64 * 1024);
        Assert.Equal(rows.SelectMany(row => row), PngReader.Read(png).Pixels);
    }

    [Theory]
    [InlineData((int)PngFilter.None, new byte[] { 10, 20, 30, 40, 60, 90 })]
    [InlineData((int)PngFilter.Sub, new byte[] { 10, 20, 30, 30, 40, 60 })]
    [InlineData((int)PngFilter.Up, new byte[] { 5, 15, 25, 246, 10, 40 })]
    [InlineData((int)PngFilter.Average, new byte[] { 8, 18, 28, 10, 25, 50 })]
    [InlineData((int)PngFilter.Paeth, new byte[] { 5, 15, 25, 246, 10, 40 })]
    public void Filter_OfAnRgbRow_PredictsEachByteAsThePngSpecificationSays(int filter, byte[] expected)
    {
        byte[] row = [10, 20, 30, 40, 60, 90];
        byte[] above = [5, 5, 5, 50, 50, 50];
        var output = new byte[row.Length];

        PngFilters.Apply((PngFilter)filter, row, above, bytesPerPixel: 3, output);

        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(2000, 1200, PngColorType.Rgb)]
    [InlineData(4500, 1100, PngColorType.Indexed)]
    [InlineData(4500, 1100, PngColorType.Grayscale)]
    public async Task Image_LargerThanOneBand_IsJoinedIntoOneStreamThatReadsBack(int width, int height, PngColorType colorType)
    {
        var format = CreateFormat(colorType);
        var rows = Enumerable.Range(0, height).Select(row => CreateTerrainRow(width * format.BytesPerPixel, row)).ToArray();

        var png = await WriteAsync(format, width, rows);

        Assert.True((long)width * format.BytesPerPixel * height > 4 << 20);
        Assert.Equal(rows.SelectMany(row => row), PngReader.Read(png).Pixels);
    }

    [Theory]
    [InlineData(PngColorType.Rgb)]
    [InlineData(PngColorType.Indexed)]
    [InlineData(PngColorType.Grayscale)]
    public async Task Image_CompressedByWorkersOfAnotherRuntime_IsTheSameAsOnTheThreadPool(PngColorType colorType)
    {
        using var services = new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();
        var workers = new ComputePngBandCompressor(new EnvelopeComputeService(services.GetRequiredService<ComputeWorkerHost>()), new JobPriority(JobPriorityClass.Background));
        var format = CreateFormat(colorType);
        var rows = Enumerable.Range(0, 1200).Select(row => CreateTerrainRow(2000 * format.BytesPerPixel, row)).ToArray();

        var onThePool = await WriteAsync(format, 2000, rows);
        var onWorkers = await WriteAsync(format, 2000, rows, workers);

        Assert.Equal(onThePool, onWorkers);
    }

    [Fact]
    public async Task Row_OfTheWrongLength_IsRefused()
    {
        using var writer = new PngWriter(new MemoryStream(), 4, 1, PngFormat.Rgb);

        await Assert.ThrowsAsync<ArgumentException>(async () => await writer.WriteRowAsync(new byte[4]));
    }

    [Fact]
    public async Task Finish_BeforeEveryRowIsWritten_IsRefused()
    {
        using var writer = new PngWriter(new MemoryStream(), 1, 2, PngFormat.Rgb);
        await writer.WriteRowAsync(new byte[3]);

        await Assert.ThrowsAsync<InvalidOperationException>(writer.FinishAsync);
    }

    private static PngFormat CreateFormat(PngColorType colorType) => colorType switch
    {
        PngColorType.Indexed => PngFormat.Indexed([.. Enumerable.Range(0, 256).Select(value => new RgbColor((byte)value, 0, 0))], transparentIndex: -1),
        PngColorType.Grayscale => PngFormat.Grayscale,
        _ => PngFormat.Rgb,
    };

    /// <summary>A row of smooth areas with sharp edges, as map layers have, that changes slowly from row to row.</summary>
    private static byte[] CreateTerrainRow(int length, int row)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
            bytes[index] = (byte)((index / 37 + row / 23) % 7 * 31 + index % 3);
        return bytes;
    }

    private static async Task<byte[]> WriteAsync(PngFormat format, int width, byte[][] rows, IPngBandCompressor? compressor = null)
    {
        using var stream = new MemoryStream();
        using (var writer = new PngWriter(stream, width, rows.Length, format, compressor ?? ThreadPoolPngBandCompressor.Instance))
        {
            foreach (var row in rows)
                await writer.WriteRowAsync(row);
            await writer.FinishAsync();
        }

        return stream.ToArray();
    }

    /// <summary>Runs each job as a Web Worker does: sent to a worker host as its envelope, and its result read back from the reply.</summary>
    private sealed class EnvelopeComputeService(ComputeWorkerHost host) : IComputeService
    {
        private const int Workers = 2;

        public int WorkerCount => Workers;

        public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
            => Task.Run(() => ComputeEnvelope.DeserializeResponse<TResult>(host.Execute(ComputeEnvelope.SerializeJob(job))), cancellationToken);
    }
}
