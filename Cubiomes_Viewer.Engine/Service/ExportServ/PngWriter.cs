using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;

using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Writes a PNG image row by row, top to bottom, so that an image never has to fit into memory at once. The rows are
/// gathered in bands, packed where the format packs pixels, which a <see cref="IPngBandCompressor"/> filters and compresses
/// several at a time, and which are joined into one zlib stream in order. The writer waits for compressed bands without
/// blocking its thread, so a single-threaded runtime can hand the bands to workers.
/// </summary>
public sealed class PngWriter : IDisposable
{
    private const int BitsPerByte = 8;
    private const byte OpaqueAlpha = byte.MaxValue;
    private const int HeaderBytes = 13;

    /// <summary>The raw bytes a band gathers before it is compressed; far smaller bands lose compression at their borders.</summary>
    private const int BandBytes = 4 << 20;

    /// <summary>The raw bytes of the bands compressed at the same time, which bounds the memory a wide image takes.</summary>
    private const long BytesInFlight = 64L << 20;

    /// <summary>The zlib header of a deflate stream with a 32 KiB window, compressed at the strongest level.</summary>
    private static ReadOnlySpan<byte> ZlibHeader => [0x78, 0xDA];

    private static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> HeaderType => "IHDR"u8;
    private static ReadOnlySpan<byte> PaletteType => "PLTE"u8;
    private static ReadOnlySpan<byte> TransparencyType => "tRNS"u8;
    private static ReadOnlySpan<byte> EndType => "IEND"u8;

    public int Width { get; }
    public int Height { get; }
    public PngFormat Format { get; }

    /// <summary>The bytes a row handed to <see cref="WriteRowAsync"/> holds.</summary>
    public int RowBytes { get; }

    private readonly int _storedRowBytes;
    private readonly Stream _destination;
    private readonly IPngBandCompressor _compressor;
    private readonly ImageDataStream _imageData;
    private readonly int _rowsPerBand;
    private readonly int _maxBandsInFlight;
    private readonly Queue<Task<PngBand>> _bands = new();
    private readonly CancellationTokenSource _stopping = new();
    private byte[] _band = [];
    private byte[] _previousRow;
    private int _rowsInBand;
    private int _rowsWritten;
    private uint _adler = Adler32.HashToUInt32([]);
    private bool _isFinished;

    /// <summary>Starts an image whose bands are compressed on the thread pool.</summary>
    public PngWriter(Stream destination, int width, int height, PngFormat format)
        : this(destination, width, height, format, ThreadPoolPngBandCompressor.Instance)
    {
    }

    internal PngWriter(Stream destination, int width, int height, PngFormat format, IPngBandCompressor compressor)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(compressor);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Format = format;
        RowBytes = checked(width * format.BytesPerPixel);
        _storedRowBytes = format.GetRowBytes(width);
        _destination = destination;
        _compressor = compressor;
        _rowsPerBand = Math.Clamp(BandBytes / _storedRowBytes, 1, height);
        _maxBandsInFlight = (int)Math.Clamp(BytesInFlight / ((long)_rowsPerBand * _storedRowBytes), 1, Math.Max(1, compressor.Concurrency));
        _previousRow = new byte[_storedRowBytes];

        destination.Write(Signature);
        WriteHeader();
        WritePalette();
        _imageData = new ImageDataStream(destination);
        _imageData.Write(ZlibHeader);
    }

    /// <summary>How many rows are compressed together; never more than the image has, as a tiled export keeps a writer per tile.</summary>
    internal int RowsPerBand => _rowsPerBand;

    /// <summary>Adds the next row; the row is copied before the first wait, so its buffer can be reused once this returns.</summary>
    public async ValueTask WriteRowAsync(ReadOnlyMemory<byte> row)
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (row.Length != RowBytes)
            throw new ArgumentException($"A row of this image holds {RowBytes} bytes, not {row.Length}.", nameof(row));
        if (_rowsWritten == Height)
            throw new InvalidOperationException($"The image has only {Height} rows.");

        if (_rowsInBand == 0)
            _band = ArrayPool<byte>.Shared.Rent(Math.Min(_rowsPerBand, Height - _rowsWritten) * _storedRowBytes);

        Store(row.Span, _band.AsSpan(_rowsInBand * _storedRowBytes, _storedRowBytes));
        _rowsInBand++;
        _rowsWritten++;
        if (_rowsInBand < _rowsPerBand && _rowsWritten < Height)
            return;

        StartBand();
        await WriteCompressedBandsAsync(untilEmpty: false).ConfigureAwait(false);
    }

    /// <summary>Ends the image once every row is written.</summary>
    public async Task FinishAsync()
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (_rowsWritten != Height)
            throw new InvalidOperationException($"{_rowsWritten} of the {Height} rows are written.");

        await WriteCompressedBandsAsync(untilEmpty: true).ConfigureAwait(false);
        WriteEnd();
        _isFinished = true;
        _stopping.Dispose();
    }

    public void Dispose()
    {
        if (_isFinished)
            return;

        _isFinished = true;
        _stopping.Cancel();
        _stopping.Dispose();
        foreach (var band in _bands)
            band.Forget();
        _bands.Clear();
    }

    /// <summary>Copies a row into the band, packing several pixels to a byte, the first in the highest bits, where the format packs them.</summary>
    private void Store(ReadOnlySpan<byte> row, Span<byte> stored)
    {
        if (Format.BitDepth == PngFormat.FullBitDepth)
        {
            row.CopyTo(stored);
            return;
        }

        stored.Clear();
        var pixelsPerByte = BitsPerByte / Format.BitDepth;
        for (var pixel = 0; pixel < row.Length; pixel++)
            stored[pixel / pixelsPerByte] |= (byte)(row[pixel] << (BitsPerByte - Format.BitDepth * (pixel % pixelsPerByte + 1)));
    }

    /// <summary>Hands the gathered rows over to be compressed.</summary>
    private void StartBand()
    {
        var band = new PngBandRows(_band, _rowsInBand, _storedRowBytes, Format.BytesPerPixel, _previousRow, Format.TriesFilters, IsLast: _rowsWritten == Height);
        _previousRow = _band.AsSpan((_rowsInBand - 1) * _storedRowBytes, _storedRowBytes).ToArray();
        _band = [];
        _rowsInBand = 0;
        _bands.Enqueue(_compressor.CompressAsync(band, _stopping.Token));
    }

    /// <summary>Writes the finished bands at the head of the queue, waiting for the oldest while too many are compressed at once.</summary>
    private async Task WriteCompressedBandsAsync(bool untilEmpty)
    {
        while (_bands.TryPeek(out var oldest) && (oldest.IsCompleted || untilEmpty || _bands.Count >= _maxBandsInFlight))
            WriteBand(await _bands.Dequeue().ConfigureAwait(false));
    }

    private void WriteBand(PngBand band)
    {
        _imageData.Write(band.Deflated);
        _adler = Adler32.Combine(_adler, band.Adler, band.RawLength);
    }

    private void WriteEnd()
    {
        Span<byte> checksum = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, _adler);
        _imageData.Write(checksum);
        _imageData.WriteChunk();
        WriteChunk(_destination, EndType, []);
    }

    private void WriteHeader()
    {
        Span<byte> header = stackalloc byte[HeaderBytes];
        header.Clear();
        BinaryPrimitives.WriteInt32BigEndian(header, Width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], Height);
        header[8] = (byte)Format.BitDepth;
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
        var crc = new Crc32();
        crc.Append(type);
        crc.Append(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, crc.GetCurrentHashAsUInt32());
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
