using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class QuadHutConditionEvaluatorTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const ulong IdealLowBits = 0x43f18;
    private const ulong BarelyLowBits = 0x1272d;
    private const uint ScannedHighs = 1 << 22;
    private const int UpperBitsTried = 2048;
    private const int BasesTried = 24;
    private const int UpperBitsPerBase = 512;
    private const int AroundTheOrigin = -1;

    private readonly ConditionTestBench _bench = new();
    private readonly CubiomesQuadFinder _finder = new();
    private readonly CubiomesStructureFinder _structureFinder = new();

    public void Dispose() => _bench.Dispose();

    [Fact]
    public void Evaluate_QuickPass_FindsTheQuadAroundTheOriginAsACandidate()
    {
        var seed = MakeSeed(IdealLowBits);

        var (status, matches) = _bench.Evaluate(Quad(QuadQuality.Ideal), Version, seed, SearchPass.Quick);

        Assert.Equal(MatchStatus.Candidates, status);
        var middle = Assert.Single(matches);
        Assert.InRange(middle.DistanceTo(new BlockPos(0, 0)), 0, 512);
    }

    [Fact]
    public void Evaluate_FullPass_MatchesExactlyWhenAllFourHutsCanGenerate()
    {
        var matched = 0;
        var tried = 0;

        foreach (var lower in FindBases(IdealLowBits).Take(BasesTried).Select(value => _finder.GetSeedOfBase(StructureType.SwampHut, Version, value, AroundTheOrigin, AroundTheOrigin)))
        {
            var huts = GetHuts(lower);
            for (long upper = 0; upper < UpperBitsPerBase; upper++)
            {
                var seed = lower | (upper << 48);
                var generator = _bench.GetGenerator(Version, seed);
                var expected = huts.All(hut => generator.CanGenerateStructure(StructureType.SwampHut, hut));

                var (status, _) = _bench.Evaluate(Quad(QuadQuality.Ideal), Version, seed, SearchPass.Full);

                Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.Failed, status);
                matched += expected ? 1 : 0;
                tried++;
            }
        }

        Assert.InRange(matched, 1, tried - 1);
    }

    [Fact]
    public void Evaluate_QuadOfAWorseClass_CountsOnlyWhenTheConditionAcceptsThatClass()
    {
        var seed = MakeSeed(BarelyLowBits);

        Assert.Equal(MatchStatus.Failed, _bench.Evaluate(Quad(QuadQuality.Normal), Version, seed, SearchPass.Quick).Status);
        Assert.Equal(MatchStatus.Candidates, _bench.Evaluate(Quad(QuadQuality.Barely), Version, seed, SearchPass.Quick).Status);
    }

    [Fact]
    public void Evaluate_QuadOutsideTheArea_DoesNotCount()
    {
        var seed = _finder.GetSeedOfBase(StructureType.SwampHut, Version, FindBase(IdealLowBits), 20, 20);

        Assert.Equal(MatchStatus.Failed, _bench.Evaluate(Quad(QuadQuality.Ideal), Version, seed, SearchPass.Quick).Status);
        Assert.Equal(MatchStatus.Candidates, _bench.Evaluate(Quad(QuadQuality.Ideal), Version, seed, SearchPass.Quick, new BlockPos(20 * 512 + 512, 20 * 512 + 512)).Status);
    }

    [Fact]
    public void Evaluate_OrdinarySeed_HasNoQuad()
        => Assert.Equal(MatchStatus.Failed, _bench.Evaluate(Quad(QuadQuality.Barely), Version, 123456789, SearchPass.Quick).Status);

    [Fact]
    public void IsMatch_SeedsOfABase_FindsWhatTheFullPassFinds()
    {
        var definition = ConditionTestBench.Define(Version, Quad(QuadQuality.Ideal));
        var plan = TestEngine.CreateSearchPlanCompiler().Compile(definition);
        var matcher = new SeedMatcher();
        var lower = MakeSeed(IdealLowBits);

        for (long upper = 0; upper < UpperBitsTried; upper += 7)
        {
            var context = _bench.Open(definition, lower | (upper << 48));
            var full = matcher.Evaluate(plan, context, SearchPass.Full);

            Assert.NotEqual(SearchOutcome.Failed, matcher.Evaluate(plan, context, SearchPass.Quick));
            Assert.Equal(full == SearchOutcome.Passed, matcher.IsMatch(plan, context));
        }
    }

    [Fact]
    public void Validate_StructureWithoutQuads_IsAProblem()
    {
        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(Version, Quad(QuadQuality.Ideal) with { Type = StructureType.Village }));

        Assert.Equal("Villages form no quads in this version.", Assert.Single(problems).Message);
    }

    private long MakeSeed(ulong lowBits) => _finder.GetSeedOfBase(StructureType.SwampHut, Version, FindBase(lowBits), AroundTheOrigin, AroundTheOrigin);

    private ulong FindBase(ulong lowBits) => FindBases(lowBits)[0];

    private ulong[] FindBases(ulong lowBits)
    {
        var bases = new ulong[4096];
        var count = _finder.ScanQuadHutBases(lowBits, 0, ScannedHighs, bases);
        Assert.True(count > 0);
        return bases[..count];
    }

    private List<BlockPos> GetHuts(long seed)
    {
        List<BlockPos> huts = [];
        foreach (var (x, z) in new[] { (-1, -1), (0, -1), (-1, 0), (0, 0) })
        {
            Assert.True(_structureFinder.TryGetRegionPosition(StructureType.SwampHut, Version, seed, x, z, out var hut));
            huts.Add(hut);
        }

        return huts;
    }

    private static QuadHutCondition Quad(QuadQuality quality) => new() { Id = 1, Quality = quality, Area = SearchArea.Square(2048) };
}
