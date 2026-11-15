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
    public static WholeImageSink Create(Stream destination, MapImageLayout layout, PngFormat format, IPngBandCompressor compressor)
        => new(new PngWriter(destination, layout.Width, layout.Height, format, compressor), null);

    /// <summary>Writes the image as one entry of an archive.</summary>
    public static WholeImageSink Create(ZipArchive archive, string entryName, MapImageLayout layout, PngFormat format, IPngBandCompressor compressor)
    {
        var stream = archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
        return new WholeImageSink(new PngWriter(stream, layout.Width, layout.Height, format, compressor), stream);
    }

    public ValueTask WriteRowAsync(ReadOnlyMemory<byte> row) => _writer.WriteRowAsync(row);

    public Task FinishAsync() => _writer.FinishAsync();

    public void Dispose()
    {
        _writer.Dispose();
        _ownedStream?.Dispose();
    }
}
