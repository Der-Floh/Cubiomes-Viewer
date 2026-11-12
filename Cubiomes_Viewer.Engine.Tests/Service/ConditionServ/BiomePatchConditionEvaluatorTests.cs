using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class BiomePatchConditionEvaluatorTests : IDisposable
{
    private const int Ocean = 0;
    private const int Plains = 1;
    private const int Forest = 4;

    private static readonly BlockPos _reference = new(-300, 900);

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    public static TheoryData<McVersion, long, int, int> Worlds() => new()
    {
        { McVersion.V1_21_3, 5, 16, Forest },
        { McVersion.V1_21_3, 123, 64, Plains },
        { McVersion.V1_16_5, 42, 16, Forest },
        { McVersion.V1_12_2, 7, 64, Ocean },
    };

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Evaluate_Patches_AreTheGroupsOfNeighbouringSamplesWithTheBiome(McVersion version, long seed, int scale, int biome)
    {
        var condition = Patches(biome, scale, minChunks: 1);
        var expected = FindPatchesOneSampleAtATime(version, seed, condition).Select(patch => patch.Middle).ToList();

        var (status, matches) = _bench.Evaluate(condition, version, seed, SearchPass.Full, _reference);

        Assert.True(expected.Count > 1);
        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal(expected.Take(64), matches);
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Evaluate_MinChunks_LeavesOutTheSmallerPatches(McVersion version, long seed, int scale, int biome)
    {
        var sizes = FindPatchesOneSampleAtATime(version, seed, Patches(biome, scale, minChunks: 1)).Select(patch => patch.Samples).ToList();
        var chunksPerSample = scale * scale / 256.0;
        var minChunks = (int)Math.Ceiling(sizes.Max() * chunksPerSample);
        var condition = Patches(biome, scale, minChunks) with { Count = 1 };
        var exclusion = condition with { MinChunks = minChunks + (int)Math.Ceiling(chunksPerSample), Count = 0 };

        var (status, matches) = _bench.Evaluate(condition, version, seed, SearchPass.Full, _reference);
        var (excluded, excludedMatches) = _bench.Evaluate(exclusion, version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal(sizes.Count(size => size == sizes.Max()), matches.Count);
        Assert.Equal(MatchStatus.Matched, excluded);
        Assert.Equal([_reference], excludedMatches);
    }

    [Theory]
    [InlineData(0, 16, "A patch covers at least one chunk.")]
    [InlineData(10, 1, "The distance between samples must be one of 4, 16, 64, 256 blocks.")]
    public void Validate_SizeOrScaleThatCannotBeSearched_IsAProblem(int minChunks, int scale, string expected)
    {
        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(McVersion.V1_21_3, Patches(Forest, scale, minChunks)));

        Assert.Equal([expected], problems.Select(problem => problem.Message));
    }

    [Fact]
    public void Evaluate_QuickPass_CannotTell()
        => Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(Patches(Forest, 16, 1), McVersion.V1_21_3, 1, SearchPass.Quick).Status);

    private static BiomePatchCondition Patches(int biome, int scale, int minChunks)
        => new() { Id = 1, Biomes = BiomeSet.Of(biome), Area = SearchArea.Circle(1500), Scale = scale, MinChunks = minChunks };

    /// <summary>
    /// Groups the neighbouring samples with the biome breadth first, starting row by row. The samples come from one area
    /// as the map draws it, since in 1.18 and later a few samples come out otherwise when asked for one by one.
    /// </summary>
    private List<(int Samples, BlockPos Middle)> FindPatchesOneSampleAtATime(McVersion version, long seed, BiomePatchCondition condition)
    {
        var scale = condition.Scale;
        var generator = _bench.GetGenerator(version, seed);
        var blocks = condition.Area.ToBlocks(_reference);
        var (left, top) = (FloorDivide(blocks.X, scale), FloorDivide(blocks.Z, scale));
        var (sizeX, sizeZ) = (FloorDivide(blocks.Right - 1, scale) - left + 1, FloorDivide(blocks.Bottom - 1, scale) - top + 1);

        BlockPos PositionOf(int x, int z) => new(
            Math.Clamp((left + x) * scale + scale / 2, blocks.X, blocks.Right - 1),
            Math.Clamp((top + z) * scale + scale / 2, blocks.Z, blocks.Bottom - 1));

        var biomes = new byte[sizeX * sizeZ];
        generator.GenerateBiomes(new BiomeArea(scale, left, top, sizeX, sizeZ, ViewSettings.DefaultSampleHeight >> 2), biomes);

        bool IsWanted(int x, int z) => condition.Biomes.Contains(biomes[z * sizeX + x]) && condition.Area.Contains(_reference, PositionOf(x, z));

        var wanted = new bool[sizeX, sizeZ];
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
                wanted[x, z] = IsWanted(x, z);
        }

        var seen = new bool[sizeX, sizeZ];
        List<(int, BlockPos)> patches = [];
        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                if (!wanted[x, z] || seen[x, z])
                    continue;

                var members = new List<(int X, int Z)>();
                var queue = new Queue<(int X, int Z)>([(x, z)]);
                seen[x, z] = true;
                while (queue.TryDequeue(out var cell))
                {
                    members.Add(cell);
                    foreach (var (nx, nz) in new[] { (cell.X - 1, cell.Z), (cell.X + 1, cell.Z), (cell.X, cell.Z - 1), (cell.X, cell.Z + 1) })
                    {
                        if (nx < 0 || nz < 0 || nx >= sizeX || nz >= sizeZ || seen[nx, nz] || !wanted[nx, nz])
                            continue;
                        seen[nx, nz] = true;
                        queue.Enqueue((nx, nz));
                    }
                }

                var positions = members.Select(member => PositionOf(member.X, member.Z)).ToList();
                patches.Add((members.Count, new BlockPos((int)(positions.Sum(position => (long)position.X) / positions.Count), (int)(positions.Sum(position => (long)position.Z) / positions.Count))));
            }
        }

        return patches;
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
