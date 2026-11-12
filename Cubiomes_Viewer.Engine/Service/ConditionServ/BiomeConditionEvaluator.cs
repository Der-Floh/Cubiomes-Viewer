using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>Biomes in an area, sampled on a grid. Biomes need the whole seed, so the quick pass cannot tell.</summary>
public sealed class BiomeConditionEvaluator : ConditionEvaluator<BiomeCondition>
{
    private const int MaxSamples = 512 * 512;
    private const int SearchCost = 50;
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

        if (!_scales.Contains(condition.Scale))
            yield return $"The distance between samples must be one of {string.Join(", ", _scales)} blocks.";
        else if (SampleGrid.CountAtMost(condition.Area, condition.Scale) > MaxSamples)
            yield return $"The area needs more than {MaxSamples} samples at {condition.Scale} blocks per sample; sample it more coarsely or make it smaller.";
    }

    protected override MatchStatus Evaluate(BiomeCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var blocks = condition.Area.ToBlocks(reference);
        var grid = SampleGrid.Create(blocks, condition.Scale, condition.Y);
        var samples = context.GetBytes(grid.SampleCount);
        context.GetGenerator(condition.Dimension).GenerateBiomes(grid, samples);

        var found = FindBiomes(condition, reference, blocks, grid, samples, out var middle);
        var holds = condition.Match == BiomeMatch.All ? found == condition.Biomes : !found.IsEmpty;
        if (holds)
            matches.Add(middle);
        return Decide(condition, reference, matches);
    }

    private string DescribeBiome(McVersion version, int biome) => _biomeCatalog.GetDisplayName(version, biome) ?? $"Biome {biome}";

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

}
