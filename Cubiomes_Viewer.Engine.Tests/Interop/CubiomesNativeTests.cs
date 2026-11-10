using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Tests.Interop;

public sealed class CubiomesNativeTests
{
    [Fact]
    public void AbiVersion_LoadedLibrary_MatchesManagedConstant()
        => Assert.Equal(Constants.NativeAbiVersion, CubiomesNative.AbiVersion());

    [Fact]
    public unsafe void DefaultBiomeColors_FullBuffer_FillsEveryChannel()
    {
        var colors = new byte[Constants.BiomeColorCount * Constants.BiomeColorChannels];

        int written;
        fixed (byte* buffer = colors)
            written = CubiomesNative.DefaultBiomeColors(buffer, colors.Length);

        Assert.Equal(colors.Length, written);
        Assert.Contains(colors, channel => channel != 0);
    }

    [Fact]
    public unsafe void DefaultBiomeColors_BufferTooSmall_ReportsInsufficientCapacity()
    {
        var colors = new byte[Constants.BiomeColorChannels];

        int result;
        fixed (byte* buffer = colors)
            result = CubiomesNative.DefaultBiomeColors(buffer, colors.Length);

        Assert.Equal((int)NativeStatus.InsufficientCapacity, result);
    }
}
