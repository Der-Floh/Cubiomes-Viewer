namespace Cubiomes_Viewer.Engine.Data.Export;

/// <summary>
/// One band of a PNG image's data, filtered and deflated, ready to follow the band before it in the image's zlib stream,
/// with the Adler-32 checksum and the length of the filtered bytes it holds.
/// </summary>
public sealed record PngBand(byte[] Deflated, uint Adler, long RawLength);
