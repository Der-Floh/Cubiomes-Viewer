using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorTests
{
    private const long AnchorSeed = 262;
    private const int MushroomFieldsBiomeId = 14;
    private const int ProbeSize = 8;

    public static TheoryData<McVersion, Dimension, int> AllVersionsDimensionsAndScales()
    {
        var data = new TheoryData<McVersion, Dimension, int>();
        foreach (var version in Enum.GetValues<McVersion>().Where(version => version != McVersion.Undefined))
            foreach (var dimension in Enum.GetValues<Dimension>())
                foreach (var scale in (int[])[1, 4, 16, 64, 256])
                    data.Add(version, dimension, scale);
        return data;
    }

    [Fact]
    public void GetBiomeAt_AnchorSeed_ReturnsMushroomFields()
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_18_2, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AnchorSeed);

        Assert.Equal(MushroomFieldsBiomeId, generator.GetBiomeAt(scale: 1, x: 0, y: 63, z: 0));
    }

    [Fact]
    public void Constructor_UndefinedVersion_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => new CubiomesWorldGenerator(McVersion.Undefined, GeneratorFlags.None));

    [Fact]
    public void GetBiomeAt_BeforeApply_ThrowsInvalidState()
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, GeneratorFlags.None);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GetBiomeAt(4, 0, 0, 0));
        Assert.Equal(NativeStatus.InvalidState, exception.Status);
    }

    [Fact]
    public void GetBiomeAt_UnsupportedScale_ThrowsInvalidArgument()
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AnchorSeed);

        var exception = Assert.Throws<CubiomesNativeException>(() => generator.GetBiomeAt(3, 0, 0, 0));
        Assert.Equal(NativeStatus.InvalidArgument, exception.Status);
    }

    [Fact]
    public void GenerateBiomes_DestinationTooSmall_ThrowsArgumentException()
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AnchorSeed);
        var area = new BiomeArea(4, 0, 0, ProbeSize, ProbeSize, 16);

        Assert.Throws<ArgumentException>(() => generator.GenerateBiomes(area, new byte[area.SampleCount - 1]));
    }

    [Fact]
    public void GetBiomeAt_AfterDispose_ThrowsObjectDisposedException()
    {
        var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, GeneratorFlags.None);
        generator.Dispose();

        Assert.Throws<ObjectDisposedException>(() => generator.GetBiomeAt(4, 0, 0, 0));
    }

    [Theory]
    [MemberData(nameof(AllVersionsDimensionsAndScales))]
    public void GenerateBiomes_AnySupportedInput_FillsTileOrReportsFailure(McVersion version, Dimension dimension, int scale)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        generator.Apply(dimension, AnchorSeed);
        var area = new BiomeArea(scale, -ProbeSize / 2, -ProbeSize / 2, ProbeSize, ProbeSize, 16);
        var tile = new byte[area.SampleCount];

        var exception = Record.Exception(() => generator.GenerateBiomes(area, tile));

        if (exception is CubiomesNativeException native)
            Assert.Equal(NativeStatus.GenerationFailed, native.Status);
        else
            Assert.Null(exception);
    }

    [Fact]
    public void GenerateBiomes_LargeBiomesFlag_ChangesTheWorld()
    {
        var area = new BiomeArea(64, -16, -16, 32, 32, 16);
        var normal = Generate(GeneratorFlags.None, area);
        var large = Generate(GeneratorFlags.LargeBiomes, area);

        Assert.NotEqual(normal, large);
        Assert.DoesNotContain(Constants.NoBiomeTileValue, normal);
    }

    private static byte[] Generate(GeneratorFlags flags, BiomeArea area)
    {
        using var generator = new CubiomesWorldGenerator(McVersion.V1_21_3, flags);
        generator.Apply(Dimension.Overworld, AnchorSeed);
        var tile = new byte[area.SampleCount];
        generator.GenerateBiomes(area, tile);
        return tile;
    }
}
