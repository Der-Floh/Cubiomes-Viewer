namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// The rows of one band of a PNG image, in a buffer rented from the shared pool that whoever compresses them gives back,
/// and the row above them, which feeds the filters of their first row; a band that tries filters is compressed with the
/// one that does best on its first rows.
/// </summary>
internal sealed record PngBandRows(byte[] Rows, int RowCount, int RowBytes, int BytesPerPixel, byte[] PreviousRow, bool TriesFilters, bool IsLast)
{
    public ReadOnlySpan<byte> Span => Rows.AsSpan(0, RowCount * RowBytes);
}
