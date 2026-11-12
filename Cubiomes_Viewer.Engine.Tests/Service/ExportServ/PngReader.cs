using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

using Cubiomes_Viewer.Engine.Data.Export;
using Cubiomes_Viewer.Engine.Service.ExportServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ExportServ;

/// <summary>A decoded PNG: its pixels row by row without filter bytes, and the palette and transparency it carries.</summary>
internal sealed record PngImage(int Width, int Height, PngColorType ColorType, byte[] Palette, byte[] Transparency, byte[] Pixels)
{
    public int BytesPerPixel => ColorType == PngColorType.Rgb ? 3 : 1;

    /// <summary>What the image shows, independent of how it was compressed.</summary>
    public string ContentHash
    {
        get
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> header = stackalloc byte[9];
            BinaryPrimitives.WriteInt32BigEndian(header, Width);
            BinaryPrimitives.WriteInt32BigEndian(header[4..], Height);
            header[8] = (byte)ColorType;
            hash.AppendData(header);
            hash.AppendData(Palette);
            hash.AppendData(Transparency);
            hash.AppendData(Pixels);
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
    }

    public ReadOnlySpan<byte> GetRow(int z) => Pixels.AsSpan(z * Width * BytesPerPixel, Width * BytesPerPixel);
}

/// <summary>Reads the PNG images the exporter writes, checking every chunk's CRC on the way.</summary>
internal static class PngReader
{
    private static ReadOnlySpan<byte> Signature => [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    public static PngImage Read(ReadOnlySpan<byte> png)
    {
        Assert.True(png[..8].SequenceEqual(Signature), "The file does not start like a PNG.");

        var (width, height, colorType) = (0, 0, PngColorType.Rgb);
        byte[] palette = [], transparency = [];
        using var imageData = new MemoryStream();
        var offset = 8;
        while (true)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png[offset..]);
            var type = Encoding.ASCII.GetString(png.Slice(offset + 4, 4));
            var data = png.Slice(offset + 8, length);
            Assert.Equal(PngCrc.Compute(png.Slice(offset + 4, 4), data), BinaryPrimitives.ReadUInt32BigEndian(png[(offset + 8 + length)..]));
            offset += 12 + length;

            switch (type)
            {
                case "IHDR":
                    (width, height, colorType) = (BinaryPrimitives.ReadInt32BigEndian(data), BinaryPrimitives.ReadInt32BigEndian(data[4..]), (PngColorType)data[9]);
                    break;
                case "PLTE":
                    palette = data.ToArray();
                    break;
                case "tRNS":
                    transparency = data.ToArray();
                    break;
                case "IDAT":
                    imageData.Write(data);
                    break;
                case "IEND":
                    Assert.Equal(png.Length, offset);
                    return new PngImage(width, height, colorType, palette, transparency, Unfilter(imageData.ToArray(), width, height, colorType == PngColorType.Rgb ? 3 : 1));
            }
        }
    }

    private static byte[] Unfilter(byte[] compressed, int width, int height, int bytesPerPixel)
    {
        using var input = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        input.CopyTo(raw);

        var rowBytes = width * bytesPerPixel;
        var filtered = raw.ToArray();
        Assert.Equal(height * (rowBytes + 1), filtered.Length);

        var pixels = new byte[height * rowBytes];
        for (var row = 0; row < height; row++)
        {
            var filter = filtered[row * (rowBytes + 1)];
            for (var index = 0; index < rowBytes; index++)
            {
                var left = index >= bytesPerPixel ? pixels[row * rowBytes + index - bytesPerPixel] : 0;
                var up = row > 0 ? pixels[(row - 1) * rowBytes + index] : 0;
                var upLeft = row > 0 && index >= bytesPerPixel ? pixels[(row - 1) * rowBytes + index - bytesPerPixel] : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"Unknown PNG filter {filter}."),
                };
                pixels[row * rowBytes + index] = unchecked((byte)(filtered[row * (rowBytes + 1) + 1 + index] + predictor));
            }
        }

        return pixels;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var (toLeft, toUp, toUpLeft) = (Math.Abs(estimate - left), Math.Abs(estimate - up), Math.Abs(estimate - upLeft));
        return toLeft <= toUp && toLeft <= toUpLeft ? left : toUp <= toUpLeft ? up : upLeft;
    }
}
