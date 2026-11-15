using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// The rows of one band of a PNG image, in a buffer rented from the shared pool that whoever compresses them gives back,
/// and the row above them, which feeds the filters of their first row.
/// </summary>
internal sealed record PngBandRows(byte[] Rows, int RowCount, int RowBytes, int BytesPerPixel, byte[] PreviousRow, PngColorType ColorType, bool IsLast)
{
    public ReadOnlySpan<byte> Span => Rows.AsSpan(0, RowCount * RowBytes);
}
