using Avalonia.Media.Imaging;

namespace Cubiomes_Viewer.Service.TileBitmapServ;

public interface ITileBitmapService
{
    /// <summary>Creates an immutable bitmap from row-ordered BGRA pixels (4 bytes per pixel).</summary>
    Bitmap Create(ReadOnlySpan<byte> bgra, int width, int height);
}
