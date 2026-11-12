using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

/// <summary>
/// A structure is placed from the lower 48 bits of the seed plus the salt of its type, so a salt raised by some amount
/// has to move structures exactly as the seed raised by that amount moves them under the game's salt.
/// </summary>
public sealed class StructureRulesTests : IDisposable
{
    private const long Seed = 2_718_281_828_459;
    private const int SaltShift = 98_765;
    private const int Regions = 3;
    private const ulong IdealLowBits = 0x43f18;
    private const uint FirstHighs = 1 << 22;

    private static readonly BlockArea _area = new(-6000, -6000, 12000, 12000);
    private static readonly BlockArea _wideArea = new(-12000, -12000, 24000, 24000);

    private readonly CubiomesStructureFinder _finder = new();
    private readonly CubiomesQuadFinder _quadFinder = new();
    private readonly List<IWorldGenerator> _generators = [];

    public void Dispose()
    {
        foreach (var generator in _generators)
            generator.Dispose();
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3)]
    [InlineData(StructureType.Village, McVersion.V1_12_2)]
    [InlineData(StructureType.DesertPyramid, McVersion.V1_16_5)]
    [InlineData(StructureType.JungleTemple, McVersion.V1_20_6)]
    [InlineData(StructureType.SwampHut, McVersion.V1_21_3)]
    [InlineData(StructureType.Igloo, McVersion.V1_14_4)]
    [InlineData(StructureType.OceanRuin, McVersion.V1_18_2)]
    [InlineData(StructureType.Shipwreck, McVersion.V1_15_2)]
    [InlineData(StructureType.Monument, McVersion.V1_21_3)]
    [InlineData(StructureType.Mansion, McVersion.V1_16_5)]
    [InlineData(StructureType.RuinedPortal, McVersion.V1_21_3)]
    [InlineData(StructureType.RuinedPortalNether, McVersion.V1_17_1)]
    [InlineData(StructureType.AncientCity, McVersion.V1_20_6)]
    [InlineData(StructureType.TrailRuins, McVersion.V1_21_3)]
    [InlineData(StructureType.TrialChambers, McVersion.V1_21_3)]
    [InlineData(StructureType.Treasure, McVersion.V1_18_2)]
    [InlineData(StructureType.Fortress, McVersion.V1_17_1)]
    [InlineData(StructureType.Fortress, McVersion.V1_21_3)]
    [InlineData(StructureType.Bastion, McVersion.V1_16_5)]
    [InlineData(StructureType.EndCity, McVersion.V1_21_3)]
    public void ReplacedSalt_MovesStructuresAsTheSeedRaisedByTheSameAmountWould(StructureType type, McVersion version)
    {
        Assert.True(_finder.TryGetGameSalt(type, version, out var gameSalt));
        var rules = Replace(type, gameSalt + SaltShift);

        var moved = GetRegionPositions(type, version, rules, Seed);

        Assert.Equal(GetRegionPositions(type, version, StructureRules.Default, Seed + SaltShift), moved);
        Assert.NotEqual(GetRegionPositions(type, version, StructureRules.Default, Seed), moved);
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3)]
    [InlineData(StructureType.Monument, McVersion.V1_12_2)]
    [InlineData(StructureType.Bastion, McVersion.V1_20_6)]
    public void GameSaltGivenAsAReplacement_ChangesNothing(StructureType type, McVersion version)
    {
        Assert.True(_finder.TryGetGameSalt(type, version, out var gameSalt));

        Assert.Equal(FindAttempts(type, version, StructureRules.Default, Seed), FindAttempts(type, version, Replace(type, gameSalt), Seed));
    }

    [Fact]
    public void ReplacedSalt_LeavesOtherStructureTypesWhereTheyAre()
    {
        var rules = Replace(StructureType.Village, 1);

        Assert.Equal(FindAttempts(StructureType.SwampHut, McVersion.V1_21_3, StructureRules.Default, Seed), FindAttempts(StructureType.SwampHut, McVersion.V1_21_3, rules, Seed));
        Assert.NotEqual(FindAttempts(StructureType.Village, McVersion.V1_21_3, StructureRules.Default, Seed), FindAttempts(StructureType.Village, McVersion.V1_21_3, rules, Seed));
    }

    [Fact]
    public void WorldWithReplacedSalt_FindsItsStructuresAmongTheMovedAttempts()
    {
        Assert.True(_finder.TryGetGameSalt(StructureType.Village, McVersion.V1_21_3, out var gameSalt));
        var rules = Replace(StructureType.Village, gameSalt + SaltShift);

        var found = Create(McVersion.V1_21_3, rules).FindStructures(StructureType.Village, _area);

        Assert.NotEmpty(found);
        Assert.Subset(FindAttempts(StructureType.Village, McVersion.V1_21_3, rules, Seed).ToHashSet(), found.ToHashSet());
        Assert.NotEqual(Create(McVersion.V1_21_3, StructureRules.Default).FindStructures(StructureType.Village, _area), found);
    }

    [Theory]
    [InlineData(StructureType.DesertPyramid)]
    [InlineData(StructureType.JungleTemple)]
    public void WithoutTerrainEstimate_KeepsTheStructuresTheEstimateLeavesOut(StructureType type)
    {
        var noEstimate = new StructureRules { EstimateTerrain = false };
        var keptMore = false;

        foreach (var seed in new[] { Seed, 1L, 262L, -77_777L })
        {
            var estimated = Create(McVersion.V1_21_3, StructureRules.Default, seed).FindStructures(type, _wideArea);
            var unestimated = Create(McVersion.V1_21_3, noEstimate, seed).FindStructures(type, _wideArea);

            Assert.Subset(unestimated.ToHashSet(), estimated.ToHashSet());
            keptMore |= unestimated.Length > estimated.Length;
        }

        Assert.True(keptMore);
    }

    [Fact]
    public void WithoutTerrainEstimate_Before118_FindsTheSameStructures()
    {
        var estimated = Create(McVersion.V1_17_1, StructureRules.Default).FindStructures(StructureType.DesertPyramid, _wideArea);
        var unestimated = Create(McVersion.V1_17_1, new StructureRules { EstimateTerrain = false }).FindStructures(StructureType.DesertPyramid, _wideArea);

        Assert.Equal(estimated, unestimated);
    }

    [Fact]
    public void ReplacedSwampHutSalt_MovesTheSeedsOfAQuadByTheSameAmount()
    {
        const McVersion version = McVersion.V1_16_5;
        var bases = new ulong[4096];
        var value = bases[.._quadFinder.ScanQuadHutBases(IdealLowBits, 0, FirstHighs, bases)][0];
        Assert.True(_finder.TryGetGameSalt(StructureType.SwampHut, version, out var gameSalt));
        var rules = Replace(StructureType.SwampHut, gameSalt + SaltShift);

        var seed = _quadFinder.GetSeedOfBase(StructureType.SwampHut, version, rules, value, 2, 3);
        Span<QuadPosition> quads = stackalloc QuadPosition[64];
        var count = _quadFinder.FindQuads(StructureType.SwampHut, version, rules, seed, -1, 0, 5, 6, quads);

        Assert.Equal(_quadFinder.GetSeedOfBase(StructureType.SwampHut, version, StructureRules.Default, value, 2, 3), StructureSeed.Of(seed + SaltShift));
        Assert.Contains(new QuadPosition(2, 3, QuadQuality.Ideal), quads[..count].ToArray());
        Assert.Equal(0, _quadFinder.FindQuads(StructureType.SwampHut, version, StructureRules.Default, seed, 2, 3, 2, 3, quads));
    }

    [Fact]
    public void StructureCondition_WithReplacedSalt_LooksAtTheMovedStructures()
    {
        using var bench = new ConditionTestBench();
        Assert.True(_finder.TryGetGameSalt(StructureType.Village, McVersion.V1_21_3, out var gameSalt));
        var condition = new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(4000) };
        var definition = ConditionTestBench.Define(McVersion.V1_21_3, condition) with { Rules = Replace(StructureType.Village, gameSalt + SaltShift) };
        var evaluator = bench.Registry.GetEvaluator(condition);
        List<BlockPos> moved = [], raised = [];

        evaluator.Evaluate(condition, ConditionTestBench.Origin, bench.Open(definition, Seed), SearchPass.Quick, moved);
        evaluator.Evaluate(condition, ConditionTestBench.Origin, bench.Open(definition with { Rules = StructureRules.Default }, Seed + SaltShift), SearchPass.Quick, raised);

        Assert.NotEmpty(moved);
        Assert.Equal(raised, moved);
    }

    [Fact]
    public void RulesForAnUnknownStructureType_AreRefused()
    {
        var rules = new StructureRules { Salts = [new StructureSalt((StructureType)99, 1)] };

        Assert.Throws<ArgumentOutOfRangeException>(() => _finder.TryGetRegionPosition(StructureType.Village, McVersion.V1_21_3, rules, Seed, 0, 0, out _));
    }

    [Theory]
    [InlineData(StructureType.Village, McVersion.V1_21_3, true)]
    [InlineData(StructureType.Fortress, McVersion.V1_16_5, true)]
    [InlineData(StructureType.EndGateway, McVersion.V1_18_2, true)]
    [InlineData(StructureType.Fortress, McVersion.V1_15_2, false)]
    [InlineData(StructureType.Mineshaft, McVersion.V1_21_3, false)]
    [InlineData(StructureType.TrialChambers, McVersion.V1_20_6, false)]
    public void TryGetGameSalt_TellsWhetherPositionsDependOnASalt(StructureType type, McVersion version, bool expected)
        => Assert.Equal(expected, _finder.TryGetGameSalt(type, version, out _));

    [Fact]
    public void Jobs_KeepTheirRulesAcrossTheWorkerBoundary()
    {
        var rules = new StructureRules { EstimateTerrain = false, Salts = [new StructureSalt(StructureType.Bastion, -3), new StructureSalt(StructureType.Village, 42)] };
        var world = new WorldKey(Seed, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.ForceOceanVariants) { Rules = rules };
        ComputeJob[] jobs =
        [
            new StructureAreaJob(new StructureAreaRequest(world, StructureType.Village, _area)),
            new SearchBatchJob(ConditionTestBench.Define(McVersion.V1_21_3, new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Square(512) }) with { Rules = rules }, Seed, 8),
        ];

        Assert.All(jobs, job => Assert.Equal(job, ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job))));
    }

    private static StructureRules Replace(StructureType type, int salt) => new() { Salts = [new StructureSalt(type, salt)] };

    private List<(bool Found, BlockPos Position)> GetRegionPositions(StructureType type, McVersion version, StructureRules rules, long seed)
    {
        List<(bool, BlockPos)> positions = [];
        for (var regionZ = -Regions; regionZ <= Regions; regionZ++)
        {
            for (var regionX = -Regions; regionX <= Regions; regionX++)
            {
                var found = _finder.TryGetRegionPosition(type, version, rules, seed, regionX, regionZ, out var position);
                positions.Add((found, position));
            }
        }

        return positions;
    }

    private BlockPos[] FindAttempts(StructureType type, McVersion version, StructureRules rules, long seed)
    {
        Assert.True(_finder.TryGetPlacement(type, version, out var placement));
        var buffer = new BlockPos[placement.CountRegions(_area)];
        return buffer[.._finder.FindAttempts(type, version, rules, seed, _area, buffer)];
    }

    private IWorldGenerator Create(McVersion version, StructureRules rules, long seed = Seed)
    {
        var generator = new CubiomesWorldGenerator(version, GeneratorFlags.ForceOceanVariants, rules);
        _generators.Add(generator);
        generator.Apply(Dimension.Overworld, seed);
        return generator;
    }
}
