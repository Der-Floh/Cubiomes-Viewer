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
