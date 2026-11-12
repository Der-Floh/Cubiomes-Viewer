using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class CubiomesQuadFinderTests
{
    private const McVersion Version = McVersion.V1_16_5;
    private const ulong IdealLowBits = 0x43f18;
    private const uint FirstHighs = 1 << 22;
    private const int HutReach = 128;
    private const long UpperBits = 0x1234L << 48;

    private readonly CubiomesQuadFinder _finder = new();
    private readonly CubiomesStructureFinder _structureFinder = new();

    [Fact]
    public void GetLowBits_EachClass_ListsCubiomesConstellationsAndEveryBetterOne()
    {
        var ideal = _finder.GetLowBits(QuadQuality.Ideal);
        var classic = _finder.GetLowBits(QuadQuality.Classic);
        var normal = _finder.GetLowBits(QuadQuality.Normal);
        var barely = _finder.GetLowBits(QuadQuality.Barely);

        Assert.Equal([3, 4, 10, 28], new[] { ideal.Count, classic.Count, normal.Count, barely.Count });
        Assert.Subset(classic.ToHashSet(), ideal.ToHashSet());
        Assert.Subset(normal.ToHashSet(), classic.ToHashSet());
        Assert.Subset(barely.ToHashSet(), normal.ToHashSet());
    }

    [Fact]
    public void ScanQuadHutBases_FirstValues_FindBasesThatAreIdealAndWithinReach()
    {
        var bases = Scan(IdealLowBits);

        Assert.NotEmpty(bases);
        Assert.All(bases, value =>
        {
            var quadBase = Assert.NotNull(_finder.DescribeQuadHutBase(value));
            Assert.Equal(QuadQuality.Ideal, quadBase.Quality);
            Assert.InRange(quadBase.Radius, 1, HutReach);
            Assert.Equal(IdealLowBits, value & 0xfffff);
        });
    }

    [Fact]
    public void DescribeQuadHutBase_ValueThatIsNoBase_ReturnsNothing()
        => Assert.Null(_finder.DescribeQuadHutBase(IdealLowBits + 1));

    [Theory]
    [InlineData(StructureType.SwampHut, -1, -1)]
    [InlineData(StructureType.SwampHut, 5, -12)]
    [InlineData(StructureType.DesertPyramid, 0, 3)]
    [InlineData(StructureType.Igloo, -7, 0)]
    public void FindQuads_SeedMadeFromABase_FindsTheQuadWhereItWasPutAndItsHutsAreClose(StructureType type, int regionX, int regionZ)
    {
        var value = Scan(IdealLowBits)[0];
        var seed = _finder.GetSeedOfBase(type, Version, StructureRules.Default, value, regionX, regionZ);
        Span<QuadPosition> quads = stackalloc QuadPosition[64];

        var count = _finder.FindQuads(type, Version, StructureRules.Default, seed | UpperBits, regionX - 3, regionZ - 3, regionX + 3, regionZ + 3, quads);

        Assert.Contains(new QuadPosition(regionX, regionZ, QuadQuality.Ideal), quads[..count].ToArray());
        var huts = GetAttempts(type, seed, regionX, regionZ);
        Assert.All(huts, first => Assert.All(huts, second => Assert.InRange(first.DistanceTo(second), 0, 2 * HutReach)));
    }

    [Fact]
    public void FindQuads_OrdinarySeed_FindsNone()
    {
        Span<QuadPosition> quads = stackalloc QuadPosition[64];

        Assert.Equal(0, _finder.FindQuads(StructureType.SwampHut, Version, StructureRules.Default, 12345, -4, -4, 4, 4, quads));
    }

    [Theory]
    [InlineData(StructureType.SwampHut, McVersion.V1_21_3, true)]
    [InlineData(StructureType.Igloo, McVersion.V1_12_2, true)]
    [InlineData(StructureType.Village, McVersion.V1_21_3, false)]
    [InlineData(StructureType.Monument, McVersion.V1_21_3, false)]
    [InlineData(StructureType.SwampHut, McVersion.Beta1_7, false)]
    public void SupportsQuads_StructureType_TellsWhetherItFormsQuads(StructureType type, McVersion version, bool expected)
        => Assert.Equal(expected, _finder.SupportsQuads(type, version));

    private ulong[] Scan(ulong lowBits)
    {
        var bases = new ulong[4096];
        return bases[.._finder.ScanQuadHutBases(lowBits, 0, FirstHighs, bases)];
    }

    private List<BlockPos> GetAttempts(StructureType type, long seed, int regionX, int regionZ)
    {
        List<BlockPos> huts = [];
        foreach (var (x, z) in new[] { (regionX, regionZ), (regionX + 1, regionZ), (regionX, regionZ + 1), (regionX + 1, regionZ + 1) })
        {
            Assert.True(_structureFinder.TryGetRegionPosition(type, Version, StructureRules.Default, seed, x, z, out var position));
            huts.Add(position);
        }

        return huts;
    }
}
