using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Biomes in an area, sampled on a grid or, for a share of the area, at random points. Biomes need the whole seed, so
/// the quick pass cannot tell.
/// </summary>
public sealed class BiomeConditionEvaluator : ConditionEvaluator<BiomeCondition>
{
    private const int MaxGridSamples = 512 * 512;
    private const int SearchCost = 50;
    private const int ShareScale = 4;
    private const int CellShift = 2;
    private const double ShareTolerance = 1e-9;
    private static readonly int[] _scales = [1, 4, 16, 64, 256];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(BiomeCondition),
        Name = "Biome",
        Description = "Biomes occur in the area.",
        Create = id => new BiomeCondition { Id = id, Biomes = BiomeSet.Empty },
        Cost = SearchCost,
    };

    private readonly IBiomeCatalog _biomeCatalog;

    public BiomeConditionEvaluator(IBiomeCatalog biomeCatalog)
    {
        _biomeCatalog = biomeCatalog;
    }

    protected override IEnumerable<string> Validate(BiomeCondition condition, SearchDefinition definition)
    {
        if (condition.Biomes.IsEmpty)
            yield return "Choose at least one biome.";

        if (!Enum.IsDefined(condition.Dimension))
            yield return "Choose the dimension the biomes are in.";
        else
        {
            foreach (var biome in condition.Biomes.ToArray().Where(biome => !_biomeCatalog.Generates(definition.Version, condition.Dimension, biome)))
                yield return $"{DescribeBiome(definition.Version, biome)} does not generate in the {condition.Dimension} of this version.";
        }

        foreach (var problem in condition.Match == BiomeMatch.Share ? ValidateShare(condition) : ValidateGrid(condition))
            yield return problem;
    }

    protected override MatchStatus Evaluate(BiomeCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var match = condition.Match == BiomeMatch.Share ? FindShare(condition, reference, context) : FindOnGrid(condition, reference, context);
        if (match is { } position)
            matches.Add(position);
        return Decide(condition, reference, matches);
    }

    private string DescribeBiome(McVersion version, int biome) => _biomeCatalog.GetDisplayName(version, biome) ?? $"Biome {biome}";

    private static IEnumerable<string> ValidateGrid(BiomeCondition condition)
    {
        if (!_scales.Contains(condition.Scale))
            yield return $"The distance between samples must be one of {string.Join(", ", _scales)} blocks.";
        else if (SampleGrid.CountAtMost(condition.Area, condition.Scale) > MaxGridSamples)
            yield return $"The area needs more than {MaxGridSamples} samples at {condition.Scale} blocks per sample; sample it more coarsely or make it smaller.";
    }

    private static IEnumerable<string> ValidateShare(BiomeCondition condition)
    {
        if (!(condition.MinShare is > 0 and <= 1))
            yield return "The biomes have to cover more than 0 % and at most 100 % of the area.";
        if (condition.Samples is < BiomeCondition.MinSamples or > BiomeCondition.MaxSamples)
            yield return $"Take from {BiomeCondition.MinSamples} to {BiomeCondition.MaxSamples} random samples.";
    }

    /// <summary>Where the wanted biomes are on the grid, when they meet the condition.</summary>
    private static BlockPos? FindOnGrid(BiomeCondition condition, BlockPos reference, SearchSeedContext context)
    {
        var blocks = condition.Area.ToBlocks(reference);
        var grid = SampleGrid.Create(blocks, condition.Scale, condition.Y);
        var samples = context.GetBytes(grid.SampleCount);
        context.GetGenerator(condition.Dimension).GenerateBiomes(grid, samples);

        var found = FindBiomes(condition, reference, blocks, grid, samples, out var middle);
        var holds = condition.Match == BiomeMatch.All ? found == condition.Biomes : !found.IsEmpty;
        return holds ? middle : null;
    }

    /// <summary>Collects the wanted biomes that occur inside the area and the middle of the samples that have one.</summary>
    private static BiomeSet FindBiomes(BiomeCondition condition, BlockPos reference, BlockArea blocks, BiomeArea grid, ReadOnlySpan<byte> samples, out BlockPos middle)
    {
        var found = BiomeSet.Empty;
        long sumX = 0, sumZ = 0, count = 0;

        for (var index = 0; index < samples.Length; index++)
        {
            var biome = samples[index];
            if (biome == Constants.NoBiomeTileValue || !condition.Biomes.Contains(biome))
                continue;

            var position = SampleGrid.GetPosition(grid, index, blocks);
            if (!condition.Area.Contains(reference, position))
                continue;

            found = found.With(biome);
            sumX += position.X;
            sumZ += position.Z;
            count++;
        }

        middle = count == 0 ? reference : new BlockPos((int)(sumX / count), (int)(sumZ / count));
        return found;
    }

    /// <summary>
    /// Samples random points until the outcome is certain: enough of them have one of the biomes, or too few are left
    /// to make up the share. The middle of the points with one of the biomes is where the share holds.
    /// </summary>
    private static BlockPos? FindShare(BiomeCondition condition, BlockPos reference, SearchSeedContext context)
    {
        var generator = context.GetGenerator(condition.Dimension);
        var points = new RandomPoints(context.Seed, condition.Id);
        var needed = Math.Max(1, (int)Math.Ceiling(condition.MinShare * condition.Samples - ShareTolerance));
        var y = condition.Y >> CellShift;
        int hits = 0, taken = 0;
        long sumX = 0, sumZ = 0;

        while (hits < needed && hits + condition.Samples - taken >= needed)
        {
            var point = points.Next(condition.Area, reference);
            taken++;
            if (!condition.Biomes.Contains(generator.GetBiomeAt(ShareScale, point.X >> CellShift, y, point.Z >> CellShift)))
                continue;

            hits++;
            sumX += point.X;
            sumZ += point.Z;
        }

        return hits >= needed ? new BlockPos((int)(sumX / hits), (int)(sumZ / hits)) : null;
    }
}
