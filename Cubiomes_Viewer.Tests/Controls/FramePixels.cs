using System.Runtime.InteropServices;

using Avalonia;
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

    /// <summary>How many pixels of a frame show exactly a colour, and the smallest rectangle around them.</summary>
    public static (int Count, PixelRect Bounds) Find(WriteableBitmap frame, RgbColor color)
    {
        var (count, left, top, right, bottom) = (0, int.MaxValue, int.MaxValue, -1, -1);
        for (var y = 0; y < frame.PixelSize.Height; y++)
        {
            for (var x = 0; x < frame.PixelSize.Width; x++)
            {
                if (Read(frame, x, y) != color)
                    continue;

                count++;
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
            }
        }

        return count == 0 ? (0, default) : (count, new PixelRect(left, top, right - left + 1, bottom - top + 1));
    }
}
