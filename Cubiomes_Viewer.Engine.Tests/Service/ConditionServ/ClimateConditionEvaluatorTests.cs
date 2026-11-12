using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class ClimateConditionEvaluatorTests : IDisposable
{
    private const McVersion Version = McVersion.V1_21_3;
    private const int Side = 1024;
    private const int Scale = 64;
    private const int Min = -1900;
    private const int Max = 3000;

    /// <summary>The area takes in both of its edges, so the last samples lie on them.</summary>
    private const int Cells = Side / Scale + 1;
    private const int FirstCell = -Side / 2 / Scale;

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    [Fact]
    public void Evaluate_EachMatch_AgreesWithTheSamplesOfTheGenerator()
    {
        List<bool> outcomes = [];

        for (var index = 0; index < 16; index++)
        {
            var seed = 77L + index * 104_729;
            var values = SampleContinentalness(seed);
            bool[] inRange = [.. values.Select(value => value is >= Min and <= Max)];

            foreach (var match in Enum.GetValues<ClimateMatch>())
            {
                var expected = match switch
                {
                    ClimateMatch.Everywhere => inRange.All(isIn => isIn),
                    ClimateMatch.Somewhere => inRange.Any(isIn => isIn),
                    _ => values.Average(value => (double)value) is >= Min and <= Max,
                };

                var (status, matches) = _bench.Evaluate(Continentalness(match), Version, seed, SearchPass.Full);

                Assert.Equal(expected ? MatchStatus.Matched : MatchStatus.Failed, status);
                Assert.Equal(expected ? 1 : 0, matches.Count);
                outcomes.Add(expected);
            }
        }

        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }

    [Fact]
    public void Evaluate_Somewhere_MatchesTheMiddleOfTheSamplesInRange()
    {
        var seed = Enumerable.Range(0, 40).Select(index => 5000L + index).First(candidate =>
        {
            var values = SampleContinentalness(candidate);
            return values.Any(value => value is >= Min and <= Max) && !values.All(value => value is >= Min and <= Max);
        });
        var values = SampleContinentalness(seed);
        BlockPos[] inRange = [.. Enumerable.Range(0, values.Length).Where(index => values[index] is >= Min and <= Max).Select(SamplePosition)];
        var expected = new BlockPos((int)(inRange.Sum(position => (long)position.X) / inRange.Length), (int)(inRange.Sum(position => (long)position.Z) / inRange.Length));

        var (_, matches) = _bench.Evaluate(Continentalness(ClimateMatch.Somewhere), Version, seed, SearchPass.Full);

        Assert.Equal([expected], matches);
    }

    [Fact]
    public void Evaluate_QuickPass_CannotTell()
    {
        Assert.Equal(MatchStatus.Unknown, _bench.Evaluate(Continentalness(ClimateMatch.Everywhere), Version, 3, SearchPass.Quick).Status);
    }

    [Theory]
    [InlineData(McVersion.V1_16_5, ClimateParameter.Continentalness, "This version has no continentalness to search for.")]
    [InlineData(McVersion.Beta1_7, ClimateParameter.Erosion, "This version has no erosion to search for.")]
    public void Validate_VersionWithoutTheParameter_IsAProblem(McVersion version, ClimateParameter parameter, string expected)
    {
        var problems = Validate(version, new ClimateCondition { Id = 1, Parameter = parameter });

        Assert.Equal([expected], problems);
    }

    [Fact]
    public void Validate_TemperatureOfABetaVersion_IsFine()
    {
        Assert.Empty(Validate(McVersion.Beta1_7, new ClimateCondition { Id = 1, Parameter = ClimateParameter.Temperature, Min = 0.5, Max = 1 }));
    }

    [Theory]
    [InlineData(0.5, 0.2, 64, "The highest value comes before the lowest one.")]
    [InlineData(-3.5, 0.2, 64, "The values lie from -3 to 3.")]
    [InlineData(double.NaN, 0.2, 64, "The values lie from -3 to 3.")]
    [InlineData(0, 0.2, 1, "The distance between samples must be one of 4, 16, 64, 256 blocks.")]
    public void Validate_RangeOrScaleThatCannotBeSearched_IsAProblem(double min, double max, int scale, string expected)
    {
        var problems = Validate(Version, new ClimateCondition { Id = 1, Min = min, Max = max, Scale = scale });

        Assert.Equal([expected], problems);
    }

    private static ClimateCondition Continentalness(ClimateMatch match) => new()
    {
        Id = 1,
        Parameter = ClimateParameter.Continentalness,
        Min = Min / 10_000.0,
        Max = Max / 10_000.0,
        Match = match,
        Area = SearchArea.Square(Side),
        Scale = Scale,
    };

    private short[] SampleContinentalness(long seed)
    {
        var values = new short[Cells * Cells];
        _bench.GetGenerator(Version, seed).GenerateClimate(ClimateParameter.Continentalness, 0, new BiomeArea(Scale, FirstCell, FirstCell, Cells, Cells, ViewSettings.DefaultSampleHeight >> 2), values);
        return values;
    }

    private static BlockPos SamplePosition(int index) => new(ToBlock(index % Cells), ToBlock(index / Cells));

    private static int ToBlock(int cell) => Math.Min((FirstCell + cell) * Scale + Scale / 2, Side / 2);

    private static List<string> Validate(McVersion version, ClimateCondition condition)
        => [.. TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(version, condition)).Select(problem => problem.Message)];
}
