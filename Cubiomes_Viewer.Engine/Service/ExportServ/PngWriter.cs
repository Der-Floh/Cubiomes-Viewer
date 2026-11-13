using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.Hashing;

using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Writes a PNG image row by row, top to bottom, so that an image never has to fit into memory at once. The rows are
/// gathered in bands, which are compressed at the strongest level in parallel where threads are available and joined into
/// one zlib stream. Indexed rows compress best unfiltered; the best filter of RGB rows depends on the layer, so each band
/// tries every filter on its first rows and is compressed with the one that did best.
/// </summary>
public sealed class PngWriter : IDisposable
{
    private const byte BitDepth = 8;
    private const byte OpaqueAlpha = byte.MaxValue;
    private const int HeaderBytes = 13;

    /// <summary>The raw bytes a band gathers before it is compressed; far smaller bands lose compression at their borders.</summary>
    private const int BandBytes = 4 << 20;

    /// <summary>The raw bytes of the bands compressed at the same time, which bounds the memory a wide image takes.</summary>
    private const long BytesInFlight = 64L << 20;

    /// <summary>The raw bytes at the start of a band that its filters are tried on; smaller samples misjudge layers whose rows compress best unfiltered.</summary>
    private const int FilterSampleBytes = 512 << 10;

    /// <summary>The zlib header of a deflate stream with a 32 KiB window, compressed at the strongest level.</summary>
    private static ReadOnlySpan<byte> ZlibHeader => [0x78, 0xDA];

    private static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static ReadOnlySpan<byte> HeaderType => "IHDR"u8;
    private static ReadOnlySpan<byte> PaletteType => "PLTE"u8;
    private static ReadOnlySpan<byte> TransparencyType => "tRNS"u8;
    private static ReadOnlySpan<byte> EndType => "IEND"u8;

    private static readonly PngFilter[] _indexedFilters = [PngFilter.None];
    private static readonly PngFilter[] _rgbFilters = [PngFilter.None, PngFilter.Sub, PngFilter.Up, PngFilter.Average, PngFilter.Paeth];

    public int Width { get; }
    public int Height { get; }
    public PngFormat Format { get; }

    /// <summary>The bytes one row holds.</summary>
    public int RowBytes { get; }

    private readonly Stream _destination;
    private readonly ImageDataStream _imageData;
    private readonly int _rowsPerBand;
    private readonly int _maxBandsInFlight;
    private readonly Queue<Task<CompressedBand>> _bands = new();
    private readonly CancellationTokenSource _stopping = new();
    private byte[] _band = [];
    private byte[] _previousRow;
    private int _rowsInBand;
    private int _rowsWritten;
    private uint _adler = Adler32.HashToUInt32([]);
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
        _rowsPerBand = Math.Clamp(BandBytes / RowBytes, 1, height);
        _maxBandsInFlight = (int)Math.Clamp(BytesInFlight / ((long)_rowsPerBand * RowBytes), 1, Environment.ProcessorCount);
        _previousRow = new byte[RowBytes];

        destination.Write(Signature);
        WriteHeader();
        WritePalette();
        _imageData = new ImageDataStream(destination);
        _imageData.Write(ZlibHeader);
    }

    /// <summary>How many rows are compressed together; never more than the image has, as a tiled export keeps a writer per tile.</summary>
    internal int RowsPerBand => _rowsPerBand;

    private PngFilter[] Filters => Format.ColorType == PngColorType.Indexed ? _indexedFilters : _rgbFilters;

    public void WriteRow(ReadOnlySpan<byte> row)
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (row.Length != RowBytes)
            throw new ArgumentException($"A row of this image holds {RowBytes} bytes, not {row.Length}.", nameof(row));
        if (_rowsWritten == Height)
            throw new InvalidOperationException($"The image has only {Height} rows.");

        if (_rowsInBand == 0)
            _band = ArrayPool<byte>.Shared.Rent(Math.Min(_rowsPerBand, Height - _rowsWritten) * RowBytes);

        row.CopyTo(_band.AsSpan(_rowsInBand * RowBytes));
        _rowsInBand++;
        _rowsWritten++;
        if (_rowsInBand == _rowsPerBand || _rowsWritten == Height)
            StartBand();
    }

    /// <summary>Ends the image once every row is written.</summary>
    public void Finish()
    {
        ObjectDisposedException.ThrowIf(_isFinished, this);
        if (_rowsWritten != Height)
            throw new InvalidOperationException($"{_rowsWritten} of the {Height} rows are written.");

        WriteCompressedBands(untilEmpty: true);
        Span<byte> checksum = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(checksum, _adler);
        _imageData.Write(checksum);
        _imageData.WriteChunk();
        WriteChunk(_destination, EndType, []);
        _isFinished = true;
        _stopping.Dispose();
    }

    public void Dispose()
    {
        if (_isFinished)
            return;

        _isFinished = true;
        _stopping.Cancel();
        // Bands still compressing are abandoned; reading their errors keeps them from surfacing as unobserved.
        foreach (var band in _bands)
            _ = band.ContinueWith(static task => task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        _bands.Clear();
    }

    /// <summary>Hands the gathered rows over to be compressed and writes the bands that are done, in order.</summary>
    private void StartBand()
    {
        var job = new BandJob(_band, _rowsInBand, RowBytes, Format.BytesPerPixel, _previousRow, Filters, IsLast: _rowsWritten == Height);
        _previousRow = _band.AsSpan((_rowsInBand - 1) * RowBytes, RowBytes).ToArray();
        _band = [];
        _rowsInBand = 0;

        if (OperatingSystem.IsBrowser())
        {
            WriteBand(job.Compress(_stopping.Token));
            return;
        }

        var stopping = _stopping.Token;
        _bands.Enqueue(Task.Run(() => job.Compress(stopping), stopping));
        WriteCompressedBands(untilEmpty: false);
    }

    /// <summary>Writes the finished bands at the head of the queue, waiting for the oldest while too many are compressed at once.</summary>
    private void WriteCompressedBands(bool untilEmpty)
    {
        while (_bands.TryPeek(out var oldest) && (oldest.IsCompleted || untilEmpty || _bands.Count >= _maxBandsInFlight))
            WriteBand(_bands.Dequeue().GetAwaiter().GetResult());
    }

    private void WriteBand(CompressedBand band)
    {
        _imageData.Write(band.Deflated.Span);
        _adler = Adler32.Combine(_adler, band.Adler, band.RawLength);
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
        var crc = new Crc32();
        crc.Append(type);
        crc.Append(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, crc.GetCurrentHashAsUInt32());
        destination.Write(number);
    }

    /// <summary>The compressed bytes of a band, ready to follow the band before it, and the checksum of what they hold.</summary>
    private readonly record struct CompressedBand(ReadOnlyMemory<byte> Deflated, uint Adler, long RawLength);

    /// <summary>
    /// Rows to compress as one band, in a rented buffer that the band gives back: the row above the band feeds the filters
    /// of its first row. Each band is compressed on its own and, unless it is the last, ends with a sync flush, so the bands
    /// can follow each other in one stream.
    /// </summary>
    private sealed record BandJob(byte[] Rows, int RowCount, int RowBytes, int BytesPerPixel, byte[] PreviousRow, PngFilter[] Filters, bool IsLast)
    {
        public CompressedBand Compress(CancellationToken cancellationToken)
        {
            var length = RowCount * (RowBytes + 1);
            var filtered = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                var filter = ChooseFilter(filtered, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Apply(filter, filtered, RowCount);
                var deflated = Deflate(filtered.AsSpan(0, length), endsTheStream: IsLast);
                return new CompressedBand(deflated.GetBuffer().AsMemory(0, (int)deflated.Length), Adler32.HashToUInt32(filtered.AsSpan(0, length)), length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(Rows);
                ArrayPool<byte>.Shared.Return(filtered);
            }
        }

        /// <summary>The filter whose rows compress smallest at the start of the band, which stands for the whole band.</summary>
        private PngFilter ChooseFilter(byte[] buffer, CancellationToken cancellationToken)
        {
            if (Filters is [var only])
                return only;

            var sampleRows = Math.Clamp(FilterSampleBytes / (RowBytes + 1), 1, RowCount);
            var (best, bestLength) = (Filters[0], long.MaxValue);
            foreach (var filter in Filters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Apply(filter, buffer, sampleRows);
                var length = Deflate(buffer.AsSpan(0, sampleRows * (RowBytes + 1)), endsTheStream: false).Length;
                if (length < bestLength)
                    (best, bestLength) = (filter, length);
            }

            return best;
        }

        private static MemoryStream Deflate(ReadOnlySpan<byte> filtered, bool endsTheStream)
        {
            var output = new MemoryStream();
            var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true);
            deflate.Write(filtered);
            deflate.Flush();
            var flushedLength = output.Length;
            deflate.Dispose();
            if (!endsTheStream)
                output.SetLength(flushedLength);
            return output;
        }

        private void Apply(PngFilter filter, byte[] filtered, int rowCount)
        {
            for (var row = 0; row < rowCount; row++)
            {
                var current = Rows.AsSpan(row * RowBytes, RowBytes);
                var above = row == 0 ? PreviousRow : Rows.AsSpan((row - 1) * RowBytes, RowBytes);
                var output = filtered.AsSpan(row * (RowBytes + 1), RowBytes + 1);
                output[0] = (byte)filter;
                PngFilters.Apply(filter, current, above, BytesPerPixel, output[1..]);
            }
        }
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
