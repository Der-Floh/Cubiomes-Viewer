using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SelfTestServ;

public sealed class ReferenceBiomeHashTests
{
    public static TheoryData<McVersion, uint> QuickOverworldHashes() => ToTheoryData(ReferenceBiomeHash.QuickOverworldHashes);

    public static TheoryData<McVersion, uint> RecordedOverworldHashes() => ToTheoryData(ReferenceBiomeHash.RecordedOverworldHashes);

    [Theory]
    [MemberData(nameof(QuickOverworldHashes))]
    public void Compute_ReferenceVersion_MatchesCubiomesTestSuite(McVersion version, uint expected)
        => Assert.Equal(expected, Compute(version));

    [Theory]
    [MemberData(nameof(RecordedOverworldHashes))]
    public void Compute_NewerVersion_MatchesTheRecordedValue(McVersion version, uint expected)
        => Assert.Equal(expected, Compute(version));

    private static uint Compute(McVersion version)
    {
        using var generator = new CubiomesWorldGenerator(version, GeneratorFlags.None);
        return ReferenceBiomeHash.Compute(generator, Dimension.Overworld, ReferenceBiomeHash.QuickBits);
    }

    private static TheoryData<McVersion, uint> ToTheoryData(IReadOnlyList<(McVersion Version, uint Hash)> hashes)
    {
        var data = new TheoryData<McVersion, uint>();
        foreach (var (version, hash) in hashes)
            data.Add(version, hash);
        return data;
    }
}
