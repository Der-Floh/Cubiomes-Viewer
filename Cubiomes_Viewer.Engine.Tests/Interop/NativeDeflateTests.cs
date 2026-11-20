using System.IO.Compression;

using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Tests.Interop;

/// <summary>Bands deflated one by one with libdeflate, joined into one stream as the PNG export joins them.</summary>
public sealed unsafe class NativeDeflateTests
{
    public static TheoryData<string, int> Bands() => new()
    {
        { "terrain", 12 },
        { "terrain", 1 },
        { "terrain", 6 },
        { "terrain", 0 },
        { "random", 12 },
        { "runs", 12 },
        { "tiny", 12 },
        { "tiny", 0 },
    };

    [Theory]
    [MemberData(nameof(Bands))]
    public void Bands_DeflatedOneByOne_InflateAsOneStream(string kind, int level)
    {
        var bands = CreateBands(kind);

        using var joined = new MemoryStream();
        for (var index = 0; index < bands.Length; index++)
            joined.Write(Deflate(bands[index], level, isLast: index == bands.Length - 1));

        Assert.Equal(bands.SelectMany(band => band), Inflate(joined.ToArray()));
    }

    [Fact]
    public void Band_ThatMoreBandsFollow_EndsOnAByteBoundaryWithAnEmptyStoredBlock()
    {
        var deflated = Deflate(CreateBands("terrain")[0], 12, isLast: false);

        Assert.Equal([0x00, 0x00, 0xFF, 0xFF], deflated[^4..]);
    }

    [Fact]
    public void Band_IntoTooSmallABuffer_IsRefused()
    {
        var data = CreateBands("random")[0];
        var output = new byte[16];

        fixed (byte* input = data, written = output)
            Assert.Equal(-3, CubiomesNative.DeflateBand(input, data.Length, 12, 0, written, output.Length));
    }

    private static byte[][] CreateBands(string kind)
    {
        var random = new Random(11);
        return kind switch
        {
            "terrain" => [.. Enumerable.Range(0, 4).Select(band => Enumerable.Range(0, 300_000).Select(index => (byte)((index / 37 + band * 5) % 7 * 31 + index % 3)).ToArray())],
            "random" => [.. Enumerable.Range(0, 3).Select(_ => { var bytes = new byte[100_000]; random.NextBytes(bytes); return bytes; })],
            "runs" => [new byte[1 << 20], [.. Enumerable.Repeat((byte)7, 70_000)], new byte[1]],
            _ => [[], [1], [1, 2, 3], new byte[17], [9, 9, 9, 9, 9, 9, 9, 9, 9, 9], []],
        };
    }

    private static byte[] Deflate(byte[] data, int level, bool isLast)
    {
        var capacity = CubiomesNative.DeflateBound(data.Length);
        var output = new byte[capacity];
        long length;
        fixed (byte* input = data, written = output)
            length = CubiomesNative.DeflateBand(input, data.Length, level, isLast ? 1 : 0, written, capacity);
        Assert.True(length > 0, $"cv_deflate_band failed with {length}.");
        return output[..(int)length];
    }

    private static byte[] Inflate(byte[] deflated)
    {
        using var inflate = new DeflateStream(new MemoryStream(deflated), CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        inflate.CopyTo(inflated);
        return inflated.ToArray();
    }
}
