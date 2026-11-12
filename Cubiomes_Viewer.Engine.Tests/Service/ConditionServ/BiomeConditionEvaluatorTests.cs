using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class BiomeConditionEvaluatorTests : IDisposable
{
    private const int NoBiome = -1;
    private const int Ocean = 0;
    private const int Plains = 1;
    private const int Forest = 4;
    private const int DeepOcean = 24;

    private static readonly BlockPos _reference = new(700, -300);

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    public static TheoryData<McVersion, long, int> WorldsAndScales() => new()
    {
        { McVersion.V1_16_5, 1, 16 },
        { McVersion.V1_16_5, -9, 4 },
        { McVersion.V1_16_5, 424242, 64 },
        { McVersion.V1_12_2, 77, 16 },
        { McVersion.V1_21_3, 1, 16 },
        { McVersion.V1_21_3, 8675309, 64 },
    };

    [Theory]
    [MemberData(nameof(WorldsAndScales))]
    public void Evaluate_AnyOfTheBiomes_MatchesWhenOneOfThemIsSampled(McVersion version, long seed, int scale)
    {
        var area = SearchArea.Square(640);
        var present = SampleBiomes(version, seed, area, scale);
        var missing = Enumerable.Range(0, 60).First(biome => !present.Contains(biome));

        var (found, matches) = _bench.Evaluate(Biomes(area, scale, BiomeSet.Of(present.First(), missing)), version, seed, SearchPass.Full, _reference);
        var (notFound, _) = _bench.Evaluate(Biomes(area, scale, BiomeSet.Of(missing)), version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, found);
        Assert.True(area.Contains(_reference, Assert.Single(matches)));
        Assert.Equal(MatchStatus.Failed, notFound);
    }

    [Theory]
    [MemberData(nameof(WorldsAndScales))]
    public void Evaluate_AllOfTheBiomes_MatchesOnlyWhenEachOfThemIsSampled(McVersion version, long seed, int scale)
    {
        var area = SearchArea.Square(640);
        var present = SampleBiomes(version, seed, area, scale);
        var missing = Enumerable.Range(0, 60).First(biome => !present.Contains(biome));
        var all = BiomeSet.Of([.. present]);

        var (complete, _) = _bench.Evaluate(Biomes(area, scale, all) with { Match = BiomeMatch.All }, version, seed, SearchPass.Full, _reference);
        var (incomplete, _) = _bench.Evaluate(Biomes(area, scale, all.With(missing)) with { Match = BiomeMatch.All }, version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, complete);
        Assert.Equal(MatchStatus.Failed, incomplete);
    }

    [Theory]
    [MemberData(nameof(WorldsAndScales))]
    public void Evaluate_Exclusion_HoldsAtItsReferencePointWhenNoneOfTheBiomesIsSampled(McVersion version, long seed, int scale)
    {
        var area = SearchArea.Square(640);
        var present = SampleBiomes(version, seed, area, scale);
        var missing = Enumerable.Range(0, 60).First(biome => !present.Contains(biome));

        var (absent, matches) = _bench.Evaluate(Biomes(area, scale, BiomeSet.Of(missing)) with { Count = 0 }, version, seed, SearchPass.Full, _reference);
        var (there, _) = _bench.Evaluate(Biomes(area, scale, BiomeSet.Of(present.First())) with { Count = 0 }, version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, absent);
        Assert.Equal([_reference], matches);
        Assert.Equal(MatchStatus.Failed, there);
    }

    [Fact]
    public void Evaluate_Circle_IgnoresTheBiomesInItsCorners()
    {
        const McVersion version = McVersion.V1_16_5;
        const int scale = 16;
        var seed = Enumerable.Range(0, 500).Select(index => (long)index).First(candidate =>
            SampleBiomes(version, candidate, SearchArea.Square(1024), scale).Except(SampleBiomes(version, candidate, SearchArea.Circle(512), scale)).Any());
        var onlyInCorners = SampleBiomes(version, seed, SearchArea.Square(1024), scale).Except(SampleBiomes(version, seed, SearchArea.Circle(512), scale)).First();

        var (square, _) = _bench.Evaluate(Biomes(SearchArea.Square(1024), scale, BiomeSet.Of(onlyInCorners)), version, seed, SearchPass.Full, _reference);
        var (circle, _) = _bench.Evaluate(Biomes(SearchArea.Circle(512), scale, BiomeSet.Of(onlyInCorners)), version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, square);
        Assert.Equal(MatchStatus.Failed, circle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(16)]
    [InlineData(256)]
    public void Evaluate_AreaOfOneBlock_SamplesTheBiomeAtThatBlock(int scale)
    {
        const McVersion version = McVersion.V1_16_5;
        const long seed = 2024;
        var block = new BlockPos(1234, -567);
        var area = SearchArea.Rectangle(0, 0, 0, 0);
        var biome = Assert.Single(SampleBiomes(version, seed, area, scale, block));

        var (status, matches) = _bench.Evaluate(Biomes(area, scale, BiomeSet.Of(biome)), version, seed, SearchPass.Full, block);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal([block], matches);
    }

    [Fact]
    public void Evaluate_QuickPass_CannotTell()
        => Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(Biomes(SearchArea.Square(64), 16, BiomeSet.Of(1)), McVersion.V1_16_5, 1, SearchPass.Quick).Status);

    [Theory]
    [InlineData(McVersion.V1_21_3)]
    [InlineData(McVersion.V1_16_5)]
    public void Evaluate_Share_AgreesWithCountingEveryRandomPoint(McVersion version)
    {
        var condition = Share(SearchArea.Circle(1500), Plains, 0.5) with { Biomes = BiomeSet.Of(Ocean, Plains, Forest, DeepOcean), Samples = 200 };
        var shares = Enumerable.Range(0, 24).Select(seed => (Seed: (long)seed, Share: (double)CountHits(condition, version, seed, condition.Samples) / condition.Samples)).ToList();
        condition = condition with { MinShare = shares.Select(entry => entry.Share).Order().ElementAt(shares.Count / 2) };
        List<bool> outcomes = [];

        foreach (var (seed, share) in shares)
        {
            var (status, matches) = _bench.Evaluate(condition, version, seed, SearchPass.Full, _reference);

            Assert.Equal(share >= condition.MinShare ? MatchStatus.Matched : MatchStatus.Failed, status);
            if (status == MatchStatus.Matched)
                Assert.True(condition.Area.Contains(_reference, Assert.Single(matches)));
            outcomes.Add(status == MatchStatus.Matched);
        }

        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }

    [Fact]
    public void Evaluate_ShareExclusion_HoldsAtItsReferencePointWhenTheBiomesCoverLess()
    {
        const McVersion version = McVersion.V1_21_3;
        var condition = Share(SearchArea.Square(2000), Plains, 0.25) with { Count = 0 };
        var seed = Enumerable.Range(0, 50).Select(index => (long)index).First(candidate => CountHits(condition, version, candidate, condition.Samples) < 100);

        var (status, matches) = _bench.Evaluate(condition, version, seed, SearchPass.Full, _reference);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal([_reference], matches);
    }

    [Fact]
    public void RandomPoints_ManyOfThem_EstimateTheShareTheGridShows()
    {
        const McVersion version = McVersion.V1_21_3;
        const int scale = 16;
        var area = SearchArea.Square(4096);

        for (var seed = 11L; seed < 14; seed++)
        {
            var onGrid = SampleGridShare(version, seed, area, scale, Plains);
            var estimate = (double)CountHits(Share(area, Plains, 0.5), version, seed, 4000) / 4000;

            Assert.InRange(estimate, onGrid - 0.05, onGrid + 0.05);
        }
    }

    [Fact]
    public void RandomPoints_OfACircle_AllLieInside()
    {
        var area = SearchArea.Circle(300);
        var points = new RandomPoints(42, 3);

        Assert.All(Enumerable.Range(0, 2000).Select(_ => points.Next(area, _reference)), point => Assert.True(area.Contains(_reference, point)));
    }

    [Theory]
    [InlineData(0, BiomeCondition.DefaultSamples, "The biomes have to cover more than 0 % and at most 100 % of the area.")]
    [InlineData(1.5, BiomeCondition.DefaultSamples, "The biomes have to cover more than 0 % and at most 100 % of the area.")]
    [InlineData(0.5, 5, "Take from 10 to 10000 random samples.")]
    public void Validate_ShareThatCannotBeJudged_IsAProblem(double minShare, int samples, string expected)
    {
        var condition = Share(SearchArea.Square(100_000), Plains, minShare) with { Samples = samples };

        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(McVersion.V1_21_3, condition));

        Assert.Equal([expected], problems.Select(problem => problem.Message));
    }

    /// <summary>How many of the first random points of a condition have one of its biomes, asked one point at a time.</summary>
    private int CountHits(BiomeCondition condition, McVersion version, long seed, int samples)
    {
        var generator = _bench.GetGenerator(version, seed);
        var points = new RandomPoints(seed, condition.Id);
        return Enumerable.Range(0, samples)
            .Select(_ => points.Next(condition.Area, _reference))
            .Count(point => condition.Biomes.Contains(generator.GetBiomeAt(4, point.X >> 2, ViewSettings.DefaultSampleHeight >> 2, point.Z >> 2)));
    }

    private double SampleGridShare(McVersion version, long seed, SearchArea area, int scale, int biome)
    {
        var blocks = area.ToBlocks(_reference);
        var grid = new BiomeArea(scale, FloorDivide(blocks.X, scale), FloorDivide(blocks.Z, scale), blocks.SizeX / scale, blocks.SizeZ / scale, ViewSettings.DefaultSampleHeight >> 2);
        var samples = new byte[grid.SampleCount];
        _bench.GetGenerator(version, seed).GenerateBiomes(grid, samples);
        return samples.Count(sample => sample == biome) / (double)samples.Length;
    }

    private static BiomeCondition Share(SearchArea area, int biome, double minShare) => new() { Id = 1, Area = area, Biomes = BiomeSet.Of(biome), Match = BiomeMatch.Share, MinShare = minShare };

    /// <summary>Asks the generator for the biome of every sample that touches the area, one sample at a time.</summary>
    private HashSet<int> SampleBiomes(McVersion version, long seed, SearchArea area, int scale, BlockPos? around = null)
    {
        var reference = around ?? _reference;
        var generator = _bench.GetGenerator(version, seed);
        var blocks = area.ToBlocks(reference);
        var y = scale == 1 ? ViewSettings.DefaultSampleHeight : ViewSettings.DefaultSampleHeight >> 2;
        HashSet<int> biomes = [];

        for (var z = FloorDivide(blocks.Z, scale); z <= FloorDivide(blocks.Bottom - 1, scale); z++)
        {
            for (var x = FloorDivide(blocks.X, scale); x <= FloorDivide(blocks.Right - 1, scale); x++)
            {
                var position = new BlockPos(
                    Math.Clamp(x * scale + scale / 2, blocks.X, blocks.Right - 1),
                    Math.Clamp(z * scale + scale / 2, blocks.Z, blocks.Bottom - 1));
                var biome = generator.GetBiomeAt(scale, x, y, z);
                if (biome != NoBiome && area.Contains(reference, position))
                    biomes.Add(biome);
            }
        }

        return biomes;
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    private static BiomeCondition Biomes(SearchArea area, int scale, BiomeSet biomes) => new() { Id = 1, Area = area, Scale = scale, Biomes = biomes };
}
