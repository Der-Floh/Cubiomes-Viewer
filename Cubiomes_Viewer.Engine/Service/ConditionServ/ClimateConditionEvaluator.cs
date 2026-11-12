using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LayerServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>A climate parameter of the Overworld in an area, sampled on a grid. It needs the whole seed, so the quick pass cannot tell.</summary>
public sealed class ClimateConditionEvaluator : ConditionEvaluator<ClimateCondition>
{
    private const int MaxSamples = 512 * 512;
    private const int SearchCost = 45;
    private static readonly int[] _scales = [4, 16, 64, 256];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(ClimateCondition),
        Name = "Climate",
        Description = "A climate parameter of the Overworld lies in a range in the area.",
        Create = id => new ClimateCondition { Id = id },
        Cost = SearchCost,
    };

    private readonly ILayerCatalog _layerCatalog;

    public ClimateConditionEvaluator(ILayerCatalog layerCatalog)
    {
        _layerCatalog = layerCatalog;
    }

    protected override IEnumerable<string> Validate(ClimateCondition condition, SearchDefinition definition)
    {
        if (!Enum.IsDefined(condition.Parameter))
            yield return "Choose a climate parameter.";
        else if (!_layerCatalog.IsAvailable(condition.Parameter.ToLayerMode(), definition.Version, Dimension.Overworld))
            yield return $"This version has no {DescribeParameter(condition, definition).ToLowerInvariant()} to search for.";

        if (!Enum.IsDefined(condition.Match))
            yield return "Choose where in the area the values have to lie.";

        if (!IsWithinLimit(condition.Min) || !IsWithinLimit(condition.Max))
            yield return $"The values lie from -{ClimateCondition.Limit} to {ClimateCondition.Limit}.";
        else if (condition.Max < condition.Min)
            yield return "The highest value comes before the lowest one.";

        if (!_scales.Contains(condition.Scale))
            yield return $"The distance between samples must be one of {string.Join(", ", _scales)} blocks.";
        else if (SampleGrid.CountAtMost(condition.Area, condition.Scale) > MaxSamples)
            yield return $"The area needs more than {MaxSamples} samples at {condition.Scale} blocks per sample; sample it more coarsely or make it smaller.";
    }

    protected override MatchStatus Evaluate(ClimateCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var blocks = condition.Area.ToBlocks(reference);
        var grid = SampleGrid.Create(blocks, condition.Scale, condition.Y);
        var values = context.GetValues(grid.SampleCount);
        context.GetGenerator(Dimension.Overworld).GenerateClimate(condition.Parameter, 0, grid, values);

        if (FindMatch(condition, reference, Tally(condition, reference, blocks, grid, values)) is { } match)
            matches.Add(match);
        return Decide(condition, reference, matches);
    }

    private string DescribeParameter(ClimateCondition condition, SearchDefinition definition)
        => _layerCatalog.GetInfo(condition.Parameter.ToLayerMode(), definition.Version, Dimension.Overworld).Name;

    private static BlockPos? FindMatch(ClimateCondition condition, BlockPos reference, SampleTally tally)
    {
        if (tally.Samples == 0)
            return null;

        return condition.Match switch
        {
            ClimateMatch.Everywhere => tally.InRange == tally.Samples ? GetAreaCenter(condition.Area, reference) : null,
            ClimateMatch.Somewhere => tally.InRange > 0 ? new BlockPos((int)(tally.SumX / tally.InRange), (int)(tally.SumZ / tally.InRange)) : null,
            ClimateMatch.OnAverage => IsInRange(condition, (double)tally.Sum / tally.Samples) ? GetAreaCenter(condition.Area, reference) : null,
            _ => throw new NotSupportedException($"Unsupported climate match: {condition.Match}"),
        };
    }

    /// <summary>Adds up the samples that lie in the area and where those in range are.</summary>
    private static SampleTally Tally(ClimateCondition condition, BlockPos reference, BlockArea blocks, BiomeArea grid, ReadOnlySpan<short> values)
    {
        long samples = 0, inRange = 0, sum = 0, sumX = 0, sumZ = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var position = SampleGrid.GetPosition(grid, index, blocks);
            if (!condition.Area.Contains(reference, position))
                continue;

            samples++;
            sum += values[index];
            if (!IsInRange(condition, values[index]))
                continue;

            inRange++;
            sumX += position.X;
            sumZ += position.Z;
        }

        return new SampleTally(samples, inRange, sum, sumX, sumZ);
    }

    private static bool IsInRange(ClimateCondition condition, double value) => value >= ToSteps(condition.Min) && value <= ToSteps(condition.Max);

    private static int ToSteps(double value) => (int)Math.Round(value * Constants.ClimateUnit);

    private static bool IsWithinLimit(double value) => Math.Abs(value) <= ClimateCondition.Limit;

    private readonly record struct SampleTally(long Samples, long InRange, long Sum, long SumX, long SumZ);
}
