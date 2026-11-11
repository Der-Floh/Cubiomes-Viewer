using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Service.TileBitmapServ;

public sealed class TileBitmapService : ITileBitmapService
{
    private static readonly Vector _dpi = new(Constants.ScreenDpi, Constants.ScreenDpi);

    public unsafe Bitmap Create(ReadOnlySpan<byte> bgra, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var stride = width * TileColorizer.BytesPerPixel;
        if (bgra.Length < stride * height)
            throw new ArgumentException($"A {width}x{height} bitmap needs {stride * height} bytes but {bgra.Length} were given.", nameof(bgra));

        fixed (byte* pixels = bgra)
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, (nint)pixels, new PixelSize(width, height), _dpi, stride);
    }
}
