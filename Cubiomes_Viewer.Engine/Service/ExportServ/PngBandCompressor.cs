using System.Buffers;
using System.IO.Compression;
using System.IO.Hashing;

using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Filters and deflates the rows of one band of a PNG image at the strongest level. Each band is deflated on its own and,
/// unless it is the last, ends with a sync flush, so that the bands can follow each other in one zlib stream. Indexed rows
/// compress best unfiltered; the best filter of RGB rows depends on the layer, so each band tries every filter on its first
/// rows and is compressed with the one that did best.
/// </summary>
internal static class PngBandCompressor
{
    /// <summary>The raw bytes at the start of a band that its filters are tried on; smaller samples misjudge layers whose rows compress best unfiltered.</summary>
    private const int FilterSampleBytes = 512 << 10;

    private static readonly PngFilter[] _indexedFilters = [PngFilter.None];
    private static readonly PngFilter[] _rgbFilters = [PngFilter.None, PngFilter.Sub, PngFilter.Up, PngFilter.Average, PngFilter.Paeth];

    public static PngBand Compress(ReadOnlySpan<byte> rows, int rowBytes, int bytesPerPixel, ReadOnlySpan<byte> previousRow, PngColorType colorType, bool isLast,
        CancellationToken cancellationToken)
    {
        var band = new BandRows(rows, rowBytes, bytesPerPixel, previousRow);
        var length = band.Count * (rowBytes + 1);
        var filtered = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var filter = ChooseFilter(band, colorType == PngColorType.Indexed ? _indexedFilters : _rgbFilters, filtered, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            band.Apply(filter, filtered, band.Count);
            var deflated = Deflate(filtered.AsSpan(0, length), endsTheStream: isLast);
            return new PngBand(deflated.ToArray(), Adler32.HashToUInt32(filtered.AsSpan(0, length)), length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(filtered);
        }
    }

    /// <summary>The filter whose rows compress smallest at the start of the band, which stands for the whole band.</summary>
    private static PngFilter ChooseFilter(BandRows band, PngFilter[] filters, byte[] buffer, CancellationToken cancellationToken)
    {
        if (filters is [var only])
            return only;

        var sampleRows = Math.Clamp(FilterSampleBytes / (band.RowBytes + 1), 1, band.Count);
        var (best, bestLength) = (filters[0], long.MaxValue);
        foreach (var filter in filters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            band.Apply(filter, buffer, sampleRows);
            var length = Deflate(buffer.AsSpan(0, sampleRows * (band.RowBytes + 1)), endsTheStream: false).Length;
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

    /// <summary>The rows of a band and the row above it, which feeds the filters of its first row.</summary>
    private readonly ref struct BandRows(ReadOnlySpan<byte> rows, int rowBytes, int bytesPerPixel, ReadOnlySpan<byte> previousRow)
    {
        private readonly ReadOnlySpan<byte> _rows = rows;
        private readonly ReadOnlySpan<byte> _previousRow = previousRow;

        public int RowBytes { get; } = rowBytes;

        public int Count => _rows.Length / RowBytes;

        /// <summary>Filters the first <paramref name="rowCount"/> rows into a buffer, each after the byte that names its filter.</summary>
        public void Apply(PngFilter filter, byte[] filtered, int rowCount)
        {
            for (var row = 0; row < rowCount; row++)
            {
                var current = _rows.Slice(row * RowBytes, RowBytes);
                var above = row == 0 ? _previousRow : _rows.Slice((row - 1) * RowBytes, RowBytes);
                var output = filtered.AsSpan(row * (RowBytes + 1), RowBytes + 1);
                output[0] = (byte)filter;
                PngFilters.Apply(filter, current, above, bytesPerPixel, output[1..]);
            }
        }
    }
}
