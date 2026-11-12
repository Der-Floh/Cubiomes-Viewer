using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class TemperatureCategoryConditionEvaluatorTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const int Side = 8192;

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    [Theory]
    [InlineData(false, 1024)]
    [InlineData(true, 4096)]
    public void Evaluate_EachCategory_MatchesTheCellsOfTheSpecialLayer(bool largeBiomes, int cellBlocks)
    {
        HashSet<TemperatureCategory> seen = [];

        for (var seed = 0L; seed < 12; seed++)
        {
            var definition = ConditionTestBench.Define(Version) with { LargeBiomes = largeBiomes };
            var area = SearchArea.Square(largeBiomes ? 4 * Side : Side);
            var first = area.MinX / cellBlocks;
            var count = area.SizeX / cellBlocks + 1;
            var cells = new byte[count * count];
            _bench.Open(definition, seed).GetGenerator(Dimension.Overworld).GenerateLayer(GeneratorLayer.TemperatureCategory, new BiomeArea(1024, first, first, count, count, 0), cells);

            foreach (var category in Enum.GetValues<TemperatureCategory>())
            {
                var condition = new TemperatureCategoryCondition { Id = 1, Category = category, Area = area };
                BlockPos[] expected = [.. Enumerable.Range(0, cells.Length).Where(index => cells[index] == (byte)category).Select(index => CellMiddle(index, first, count, cellBlocks, area))];

                var (status, matches) = Evaluate(condition, definition, seed);

                Assert.Equal(expected.Length > 0 ? MatchStatus.Matched : MatchStatus.Failed, status);
                Assert.Equal(expected, matches);
                if (expected.Length > 0)
                    seen.Add(category);
            }
        }

        Assert.Equal(Enum.GetValues<TemperatureCategory>().Length, seen.Count);
    }

    [Theory]
    [InlineData(false, 1024)]
    [InlineData(true, 4096)]
    public void SpecialLushCells_HoldFarMoreJungleThanOtherCellsAtTheirSize(bool largeBiomes, int cellBlocks)
    {
        const int scale = 64;
        const int cellsPerSide = 8;
        int[] jungles = [21, 22, 23, 149, 151, 168, 169];
        var definition = ConditionTestBench.Define(Version) with { LargeBiomes = largeBiomes };
        var samplesPerCell = cellBlocks / scale;
        List<double> special = [];
        List<double> other = [];

        for (var seed = 0L; seed < 10; seed++)
        {
            var generator = _bench.Open(definition, seed).GetGenerator(Dimension.Overworld);
            var cells = new byte[cellsPerSide * cellsPerSide];
            generator.GenerateLayer(GeneratorLayer.TemperatureCategory, new BiomeArea(1024, 0, 0, cellsPerSide, cellsPerSide, 0), cells);
            var side = cellsPerSide * samplesPerCell;
            var biomes = new byte[side * side];
            generator.GenerateBiomes(new BiomeArea(scale, 0, 0, side, side, 16), biomes);

            for (var index = 0; index < cells.Length; index++)
            {
                var (cellX, cellZ) = (index % cellsPerSide, index / cellsPerSide);
                var jungle = 0;
                for (var z = 0; z < samplesPerCell; z++)
                {
                    for (var x = 0; x < samplesPerCell; x++)
                        jungle += jungles.Contains(biomes[(cellZ * samplesPerCell + z) * side + cellX * samplesPerCell + x]) ? 1 : 0;
                }

                (cells[index] == (byte)TemperatureCategory.SpecialLush ? special : other).Add(jungle / (double)(samplesPerCell * samplesPerCell));
            }
        }

        Assert.NotEmpty(special);
        Assert.True(special.Average() > 4 * other.Average(), $"Special lush cells hold {special.Average():P0} jungle, the others {other.Average():P0}.");
    }

    [Fact]
    public void Evaluate_Count_AsksForThatManyCells()
    {
        var condition = new TemperatureCategoryCondition { Id = 1, Category = TemperatureCategory.Warm, Area = SearchArea.Square(Side) };
        var definition = ConditionTestBench.Define(Version);
        var checkedSeeds = 0;

        for (var seed = 0L; seed < 12; seed++)
        {
            var found = Evaluate(condition, definition, seed).Matches.Count;
            if (found == 0)
                continue;

            Assert.Equal(MatchStatus.Matched, Evaluate(condition with { Count = found }, definition, seed).Status);
            Assert.Equal(MatchStatus.Failed, Evaluate(condition with { Count = found + 1 }, definition, seed).Status);
            checkedSeeds++;
        }

        Assert.True(checkedSeeds > 0);
    }

    [Fact]
    public void Evaluate_QuickPass_CannotTell()
        => Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(new TemperatureCategoryCondition { Id = 1 }, Version, 1, SearchPass.Quick).Status);

    [Theory]
    [InlineData(McVersion.V1_6_4)]
    [InlineData(McVersion.V1_18_2)]
    public void Validate_VersionWithoutTheLayer_IsAProblem(McVersion version)
    {
        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(version, new TemperatureCategoryCondition { Id = 1 }));

        Assert.Equal(["Temperature categories decide the biomes of 1.7 to 1.17 only."], problems.Select(problem => problem.Message));
    }

    private (MatchStatus Status, List<BlockPos> Matches) Evaluate(TemperatureCategoryCondition condition, SearchDefinition definition, long seed)
    {
        List<BlockPos> matches = [];
        var status = _bench.Registry.GetEvaluator(condition).Evaluate(condition, ConditionTestBench.Origin, _bench.Open(definition with { Conditions = [condition] }, seed), SearchPass.Full, matches);
        return (status, matches);
    }

    /// <summary>The middle of a cell, moved onto the edge of the area where the cell reaches beyond it.</summary>
    private static BlockPos CellMiddle(int index, int first, int count, int cellBlocks, SearchArea area)
        => new(Math.Clamp((first + index % count) * cellBlocks + cellBlocks / 2, area.MinX, area.MaxX), Math.Clamp((first + index / count) * cellBlocks + cellBlocks / 2, area.MinZ, area.MaxZ));
}
