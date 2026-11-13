using System.Runtime.InteropServices;

using Avalonia.Media.Imaging;
using Avalonia.Platform;

using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Tests.Controls;

/// <summary>Reads the colours of a frame captured from a headless window.</summary>
internal static class FramePixels
{
    private const int BytesPerPixel = 4;

    public static RgbColor Read(WriteableBitmap frame, int x, int y)
    {
        using var buffer = frame.Lock();
        var offset = y * buffer.RowBytes + x * BytesPerPixel;
        var first = Marshal.ReadByte(buffer.Address, offset);
        var green = Marshal.ReadByte(buffer.Address, offset + 1);
        var third = Marshal.ReadByte(buffer.Address, offset + 2);

        return buffer.Format == PixelFormat.Bgra8888
            ? new RgbColor(third, green, first)
            : new RgbColor(first, green, third);
    }
}
