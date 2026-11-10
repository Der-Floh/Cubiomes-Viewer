using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class CubiomesSlimeChunkFinderTests
{
    private const long JavaMultiplier = 0x5DEECE66DL;
    private const long JavaAddend = 0xBL;
    private const long JavaMask = (1L << 48) - 1;

    private readonly CubiomesSlimeChunkFinder _finder = new();

    [Theory]
    [InlineData(0L)]
    [InlineData(262L)]
    [InlineData(-4172144997902289642L)]
    public void FindSlimeChunks_AnySeed_MatchesTheJavaFormula(long seed)
    {
        const int originX = -37, originZ = 1021, sizeX = 96, sizeZ = 64;
        var chunks = new byte[sizeX * sizeZ];

        _finder.FindSlimeChunks(seed, originX, originZ, sizeX, sizeZ, chunks);

        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
                Assert.Equal(IsSlimeChunk(seed, originX + x, originZ + z), chunks[z * sizeX + x] == 1);
        }

        Assert.InRange(chunks.Count(chunk => chunk == 1), sizeX * sizeZ / 20, sizeX * sizeZ / 5);
    }

    [Fact]
    public void FindSlimeChunks_DestinationTooSmall_ThrowsInsufficientCapacity()
    {
        var exception = Assert.Throws<CubiomesNativeException>(() => _finder.FindSlimeChunks(1, 0, 0, 4, 4, new byte[15]));

        Assert.Equal(NativeStatus.InsufficientCapacity, exception.Status);
    }

    /// <summary>The check Minecraft itself performs, written against java.util.Random.</summary>
    private static bool IsSlimeChunk(long seed, int chunkX, int chunkZ)
    {
        unchecked
        {
            var scrambled = seed
                + chunkX * chunkX * 4987142
                + chunkX * 5947611
                + chunkZ * chunkZ * 4392871L
                + chunkZ * 389711
                ^ 987234911L;
            return NextInt(scrambled, 10) == 0;
        }
    }

    private static int NextInt(long seed, int bound)
    {
        var state = (seed ^ JavaMultiplier) & JavaMask;
        while (true)
        {
            state = (state * JavaMultiplier + JavaAddend) & JavaMask;
            var bits = (int)(state >>> 17);
            var value = bits % bound;
            if (bits - value + (bound - 1) >= 0)
                return value;
        }
    }
}
