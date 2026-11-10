using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SelfTestServ;

public sealed class ReferenceBiomeHashTests
{
    public static TheoryData<McVersion, uint> QuickOverworldHashes()
    {
        var data = new TheoryData<McVersion, uint>();
        foreach (var (version, hash) in ReferenceBiomeHash.QuickOverworldHashes)
            data.Add(version, hash);
        return data;
    }

    [Theory]
    [MemberData(nameof(QuickOverworldHashes))]
    public void Compute_ReferenceVersion_MatchesCubiomesTestSuite(McVersion version, uint expected)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);

        var actual = ReferenceBiomeHash.Compute(generator, Dimension.Overworld, ReferenceBiomeHash.QuickBits);

        Assert.Equal(expected, actual);
    }
}
