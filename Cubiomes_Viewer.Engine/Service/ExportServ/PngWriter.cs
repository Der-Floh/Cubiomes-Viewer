using System.Buffers.Binary;
using System.IO.Compression;

using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Writes a PNG image row by row, top to bottom, so that an image never has to fit into memory at once.
/// Indexed rows are stored unfiltered, RGB rows as the difference to the row above.
/// </summary>
public sealed class PngWriter : IDisposable
{
    private const byte NoFilter = 0;
    private const byte UpFilter = 2;
    private const byte BitDepth = 8;
    private const byte OpaqueAlpha = byte.MaxValue;
    private const int HeaderBytes = 13;

    private static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> HeaderType => "IHDR"u8;
    private static ReadOnlySpan<byte> PaletteType => "PLTE"u8;
    private static ReadOnlySpan<byte> TransparencyType => "tRNS"u8;
    private static ReadOnlySpan<byte> EndType => "IEND"u8;

    public int Width { get; }
    public int Height { get; }
    public PngFormat Format { get; }

    /// <summary>The bytes one row holds.</summary>
    public int RowBytes { get; }

    private readonly Stream _destination;
    private readonly ImageDataStream _imageData;
    private readonly ZLibStream _compressor;
    private readonly byte[] _filtered;
    private readonly byte[] _previous;
    private int _rowsWritten;
    private bool _isFinished;

    public PngWriter(Stream destination, int width, int height, PngFormat format)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Format = format;
        RowBytes = checked(width * format.BytesPerPixel);
        _destination = destination;
        _filtered = new byte[RowBytes + 1];
        _previous = new byte[RowBytes];

        destination.Write(Signature);
        WriteHeader();
        WritePalette();
        _imageData = new ImageDataStream(destination);
        _compressor = new ZLibStream(_imageData, CompressionLevel.Optimal, leaveOpen: true);
    }

    public void WriteRow(ReadOnlySpan<byte> row)
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (row.Length != RowBytes)
            throw new ArgumentException($"A row of this image holds {RowBytes} bytes, not {row.Length}.", nameof(row));
        if (_rowsWritten == Height)
            throw new InvalidOperationException($"The image has only {Height} rows.");

        if (Format.ColorType == PngColorType.Indexed)
        {
            _filtered[0] = NoFilter;
            row.CopyTo(_filtered.AsSpan(1));
        }
        else
        {
            _filtered[0] = UpFilter;
            for (var index = 0; index < row.Length; index++)
                _filtered[index + 1] = unchecked((byte)(row[index] - _previous[index]));
            row.CopyTo(_previous);
        }

        _compressor.Write(_filtered);
        _rowsWritten++;
    }

    /// <summary>Ends the image once every row is written.</summary>
    public void Finish()
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (_rowsWritten != Height)
            throw new InvalidOperationException($"{_rowsWritten} of the {Height} rows are written.");

        _compressor.Dispose();
        _imageData.WriteChunk();
        WriteChunk(_destination, EndType, []);
        _isFinished = true;
    }

    public void Dispose()
    {
        if (_isFinished)
            return;

        _compressor.Dispose();
        _isFinished = true;
    }

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], Height);
        header[8] = BitDepth;
        header[9] = (byte)Format.ColorType;
        WriteChunk(_destination, HeaderType, header);
    }

    private void WritePalette()
    {
        if (Format.ColorType != PngColorType.Indexed)
            return;

        var palette = new byte[Format.Palette.Count * 3];
        for (var index = 0; index < Format.Palette.Count; index++)
        {
            var color = Format.Palette[index];
            (palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2]) = (color.Red, color.Green, color.Blue);
        }

        WriteChunk(_destination, PaletteType, palette);
        if (Format.TransparentIndex < 0)
            return;

        var alpha = new byte[Format.TransparentIndex + 1];
        alpha.AsSpan().Fill(OpaqueAlpha);
        alpha[Format.TransparentIndex] = 0;
        WriteChunk(_destination, TransparencyType, alpha);
    }

    private static void WriteChunk(Stream destination, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        destination.Write(number);
        destination.Write(type);
        destination.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, PngCrc.Compute(type, data));
        destination.Write(number);
    }

    /// <summary>Cuts the compressed image data into IDAT chunks of a bounded size as it arrives.</summary>
    private sealed class ImageDataStream(Stream destination) : Stream
    {
        private const int ChunkBytes = 64 * 1024;

        private static ReadOnlySpan<byte> DataType => "IDAT"u8;

        private readonly byte[] _buffer = new byte[ChunkBytes];
        private int _count;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            while (!buffer.IsEmpty)
            {
                var taken = Math.Min(buffer.Length, ChunkBytes - _count);
                buffer[..taken].CopyTo(_buffer.AsSpan(_count));
                _count += taken;
                buffer = buffer[taken..];
                if (_count == ChunkBytes)
                    WriteChunk();
            }
        }

        /// <summary>Writes what has arrived since the last chunk as one more chunk.</summary>
        public void WriteChunk()
        {
            if (_count == 0)
                return;

            PngWriter.WriteChunk(destination, DataType, _buffer.AsSpan(0, _count));
            _count = 0;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
