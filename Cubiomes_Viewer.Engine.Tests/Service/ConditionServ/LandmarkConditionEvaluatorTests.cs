using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

/// <summary>The conditions on what a world has only once or a fixed number of: its spawn point, its strongholds and its slime chunks.</summary>
public sealed class LandmarkConditionEvaluatorTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const int ChunkBlocks = 16;
    private const int StrongholdCount = 128;

    private readonly ConditionTestBench _bench = new();
    private readonly CubiomesSlimeChunkFinder _slimeChunkFinder = new();

    public void Dispose() => _bench.Dispose();

    public static TheoryData<long> Seeds() => [3, -77, 123456789, 5_000_000_000_017, long.MinValue + 99];

    public static TheoryData<McVersion, long> RingVersionsAndSeeds() => new()
    {
        { McVersion.V1_9_4, 11 },
        { McVersion.V1_12_2, 12 },
        { McVersion.V1_12_2, -4096 },
        { McVersion.V1_16_5, 13 },
        { McVersion.V1_16_5, 987654321987 },
        { McVersion.V1_19_2, 14 },
        { McVersion.V1_21_3, 15 },
    };

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Spawn_FullPass_MatchesWhereTheSpawnPointLies(long seed)
    {
        var spawn = Assert.NotNull(_bench.GetGenerator(Version, seed).FindSpawn());
        var around = new SpawnCondition { Id = 1, Area = SearchArea.Circle(100) };

        var (near, matches) = _bench.Evaluate(around, Version, seed, SearchPass.Full, new BlockPos(spawn.X + 60, spawn.Z - 80));
        var (far, _) = _bench.Evaluate(around, Version, seed, SearchPass.Full, new BlockPos(spawn.X + 61, spawn.Z - 80));

        Assert.Equal(MatchStatus.Matched, near);
        Assert.Equal([spawn], matches);
        Assert.Equal(MatchStatus.Failed, far);
    }

    [Fact]
    public void Spawn_QuickPass_CannotTell()
        => Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(new SpawnCondition { Id = 1 }, Version, 3, SearchPass.Quick).Status);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Stronghold_FullPass_FindsTheStrongholdsOfTheAreaHoweverFarItReaches(long seed)
    {
        var strongholds = _bench.GetGenerator(Version, seed).FindStrongholds();
        SearchArea[] areas = [SearchArea.Circle(1500), SearchArea.Circle(3072), SearchArea.Square(12_000), SearchArea.Rectangle(-30_000, -30_000, 30_000, 30_000)];

        Assert.Equal(StrongholdCount, strongholds.Length);
        foreach (var area in areas)
        {
            foreach (var reference in new[] { ConditionTestBench.Origin, new BlockPos(4000, -2500) })
            {
                var condition = new StrongholdCondition { Id = 1, Area = area };
                var expected = strongholds.Where(stronghold => area.Contains(reference, stronghold)).ToList();

                var (status, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full, reference);

                Assert.Equal(expected.Count > 0 ? MatchStatus.Matched : MatchStatus.Failed, status);
                Assert.Equal(expected, matches);
            }
        }
    }

    [Theory]
    [MemberData(nameof(RingVersionsAndSeeds))]
    public void Stronghold_EveryRing_LiesBeyondTheDistanceTheEvaluatorSkipsItAt(McVersion version, long seed)
    {
        var strongholds = _bench.GetGenerator(version, seed).FindStrongholds();

        Assert.Equal(StrongholdCount, strongholds.Length);
        for (var index = 0; index < strongholds.Length; index++)
        {
            var distance = strongholds[index].DistanceTo(ConditionTestBench.Origin);
            Assert.True(StrongholdConditionEvaluator.CountStrongholdsWithin(version, distance) > index, $"Stronghold {index} at {strongholds[index]} would be skipped.");
        }
    }

    [Theory]
    [InlineData(McVersion.V1_21_3, 1151, 0)]
    [InlineData(McVersion.V1_21_3, 1152, 3)]
    [InlineData(McVersion.V1_21_3, 4223, 3)]
    [InlineData(McVersion.V1_21_3, 4224, 9)]
    [InlineData(McVersion.V1_21_3, 1_000_000, 128)]
    [InlineData(McVersion.V1_8_9, 0, 3)]
    public void CountStrongholdsWithin_Distance_CountsTheRingsThatReachIt(McVersion version, double blocks, int expected)
        => Assert.Equal(expected, StrongholdConditionEvaluator.CountStrongholdsWithin(version, blocks));

    [Fact]
    public void Stronghold_QuickPass_CannotTell()
        => Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(new StrongholdCondition { Id = 1 }, Version, 3, SearchPass.Quick).Status);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void SlimeChunks_EveryPass_CountsTheChunksWhoseCornerLiesInTheArea(long seed)
    {
        var reference = new BlockPos(-100, 250);
        var area = SearchArea.Rectangle(-70, -40, 90, 130);
        var expected = CountSlimeChunks(seed, area, reference);

        foreach (var pass in Enum.GetValues<SearchPass>())
        {
            Assert.Equal(MatchStatus.Matched, _bench.Evaluate(SlimeChunks(area, expected), Version, seed, pass, reference).Status);
            Assert.Equal(MatchStatus.Failed, _bench.Evaluate(SlimeChunks(area, expected + 1), Version, seed, pass, reference).Status);
            Assert.Equal(expected == 0 ? MatchStatus.Matched : MatchStatus.Failed, _bench.Evaluate(SlimeChunks(area, 0), Version, seed, pass, reference).Status);
        }
    }

    [Fact]
    public void SlimeChunks_Match_IsTheMiddleOfTheArea()
    {
        var reference = new BlockPos(1000, 2000);

        var (_, matches) = _bench.Evaluate(SlimeChunks(SearchArea.Rectangle(-100, -20, 300, 60), 1), Version, 3, SearchPass.Quick, reference);

        Assert.Equal([new BlockPos(1100, 2020)], matches);
    }

    [Fact]
    public void SlimeChunks_AreaBetweenChunkCorners_HasNone()
    {
        var area = SearchArea.Rectangle(1, 1, 15, 15);

        Assert.Equal(MatchStatus.Failed, _bench.Evaluate(SlimeChunks(area, 1), Version, 3, SearchPass.Quick).Status);
        Assert.Equal(MatchStatus.Matched, _bench.Evaluate(SlimeChunks(area, 0), Version, 3, SearchPass.Quick).Status);
    }

    private int CountSlimeChunks(long seed, SearchArea area, BlockPos reference)
    {
        var count = 0;
        Span<byte> chunk = stackalloc byte[1];
        for (var chunkZ = -64; chunkZ < 64; chunkZ++)
        {
            for (var chunkX = -64; chunkX < 64; chunkX++)
            {
                _slimeChunkFinder.FindSlimeChunks(seed, chunkX, chunkZ, 1, 1, chunk);
                if (chunk[0] == 1 && area.Contains(reference, new BlockPos(chunkX * ChunkBlocks, chunkZ * ChunkBlocks)))
                    count++;
            }
        }

        return count;
    }

    private static SlimeChunkCondition SlimeChunks(SearchArea area, int count) => new() { Id = 1, Area = area, Count = count };
}
