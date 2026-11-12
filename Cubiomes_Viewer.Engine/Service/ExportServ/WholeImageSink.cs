using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>Writes the rows as one PNG image.</summary>
internal sealed class WholeImageSink : IImageRowSink
{
    private readonly PngWriter _writer;
    private readonly Stream? _ownedStream;

    private WholeImageSink(PngWriter writer, Stream? ownedStream)
    {
        _writer = writer;
        _ownedStream = ownedStream;
    }

    /// <summary>Writes the image into a stream, which stays open.</summary>
    public static WholeImageSink Create(Stream destination, MapImageLayout layout, PngFormat format)
        => new(new PngWriter(destination, layout.Width, layout.Height, format), null);

    /// <summary>Writes the image as one entry of an archive.</summary>
    public static WholeImageSink Create(ZipArchive archive, string entryName, MapImageLayout layout, PngFormat format)
    {
        var stream = archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
        return new WholeImageSink(new PngWriter(stream, layout.Width, layout.Height, format), stream);
    }

    public void WriteRow(ReadOnlySpan<byte> row) => _writer.WriteRow(row);

    public void Finish() => _writer.Finish();

    public void Dispose()
    {
        _writer.Dispose();
        _ownedStream?.Dispose();
    }
}
