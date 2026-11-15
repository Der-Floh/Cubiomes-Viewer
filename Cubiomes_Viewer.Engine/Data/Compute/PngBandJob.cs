using Cubiomes_Viewer.Engine.Data.Export;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// Rows of a PNG image to filter and deflate as one band of its data, for workers that the exporting thread cannot share
/// work with, such as Web Workers. The row above the band feeds the filters of its first row.
/// </summary>
public sealed record PngBandJob(byte[] Rows, int RowBytes, int BytesPerPixel, byte[] PreviousRow, PngColorType ColorType, bool IsLast) : ComputeJob<PngBand>;
