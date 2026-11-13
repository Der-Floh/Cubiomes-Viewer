using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Service.ExportServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ExportServ;

public sealed class PngWriterTests
{
    [Fact]
    public void Crc_OfAnEmptyEndChunk_IsTheOneEveryPngEndsWith()
        => Assert.Equal(0xAE426082u, PngCrc.Compute("IEND"u8, []));

    [Fact]
    public void RgbImage_ReadsBackRowByRow()
    {
        byte[][] rows = [[1, 2, 3, 250, 251, 252], [0, 0, 0, 255, 255, 255], [9, 8, 7, 1, 1, 1]];

        var image = PngReader.Read(Write(PngFormat.Rgb, 2, rows));

        Assert.Equal((2, 3, PngColorType.Rgb), (image.Width, image.Height, image.ColorType));
        Assert.Equal(rows.SelectMany(row => row), image.Pixels);
    }

    [Fact]
    public void IndexedImage_KeepsItsPaletteAndTheTransparentEntry()
    {
        var format = PngFormat.Indexed([new RgbColor(10, 20, 30), new RgbColor(40, 50, 60), new RgbColor(70, 80, 90)], transparentIndex: 2);
        byte[][] rows = [[0, 1, 2, 1], [2, 2, 0, 0]];

        var image = PngReader.Read(Write(format, 4, rows));

        Assert.Equal(PngColorType.Indexed, image.ColorType);
        Assert.Equal([10, 20, 30, 40, 50, 60, 70, 80, 90], image.Palette);
        Assert.Equal([255, 255, 0], image.Transparency);
        Assert.Equal(rows.SelectMany(row => row), image.Pixels);
    }

    [Fact]
    public void LargeImage_IsCutIntoSeveralDataChunksAndStillReadsBack()
    {
        var random = new Random(7);
        var rows = Enumerable.Range(0, 300).Select(_ => { var row = new byte[900]; random.NextBytes(row); return row; }).ToArray();

        var png = Write(PngFormat.Rgb, 300, rows);

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
    [InlineData(2000, 1200, false)]
    [InlineData(4500, 1100, true)]
    public void Image_LargerThanOneBand_IsJoinedIntoOneStreamThatReadsBack(int width, int height, bool isIndexed)
    {
        var format = isIndexed ? PngFormat.Indexed([.. Enumerable.Range(0, 256).Select(value => new RgbColor((byte)value, 0, 0))], transparentIndex: -1) : PngFormat.Rgb;
        var rows = Enumerable.Range(0, height).Select(row => CreateTerrainRow(width * format.BytesPerPixel, row)).ToArray();

        var png = Write(format, width, rows);

        Assert.True((long)width * format.BytesPerPixel * height > 4 << 20);
        Assert.Equal(rows.SelectMany(row => row), PngReader.Read(png).Pixels);
    }

    [Fact]
    public void Adler32_OfTwoPartsCombined_IsTheChecksumOfBothInARow()
    {
        var random = new Random(3);
        var first = new byte[12_345];
        var second = new byte[7_000];
        random.NextBytes(first);
        random.NextBytes(second);

        var combined = Adler32.Combine(Adler32.Compute(first), Adler32.Compute(second), second.Length);

        Assert.Equal(0x11E60398u, Adler32.Compute("Wikipedia"u8));
        Assert.Equal(Adler32.Compute([.. first, .. second]), combined);
    }

    [Fact]
    public void Row_OfTheWrongLength_IsRefused()
    {
        using var writer = new PngWriter(new MemoryStream(), 4, 1, PngFormat.Rgb);

        Assert.Throws<ArgumentException>(() => writer.WriteRow(new byte[4]));
    }

    [Fact]
    public void Finish_BeforeEveryRowIsWritten_IsRefused()
    {
        using var writer = new PngWriter(new MemoryStream(), 1, 2, PngFormat.Rgb);
        writer.WriteRow(new byte[3]);

        Assert.Throws<InvalidOperationException>(writer.Finish);
    }

    /// <summary>A row of smooth areas with sharp edges, as map layers have, that changes slowly from row to row.</summary>
    private static byte[] CreateTerrainRow(int length, int row)
    {
        var bytes = new byte[length];
        for (var index = 0; index < length; index++)
            bytes[index] = (byte)((index / 37 + row / 23) % 7 * 31 + index % 3);
        return bytes;
    }

    private static byte[] Write(PngFormat format, int width, byte[][] rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new PngWriter(stream, width, rows.Length, format))
        {
            foreach (var row in rows)
                writer.WriteRow(row);
            writer.Finish();
        }

        return stream.ToArray();
    }
}
