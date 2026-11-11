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
    private const int VerticalCellShift = 2;
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

        foreach (var biome in condition.Biomes.ToArray().Where(biome => _biomeCatalog.GetName(definition.Version, biome) is null))
            yield return $"Biome {biome} does not exist in this version.";

        if (!_scales.Contains(condition.Scale))
            yield return $"The distance between samples must be one of {string.Join(", ", _scales)} blocks.";
        else if (CountSamplesAtMost(condition) > MaxSamples)
            yield return $"The area needs more than {MaxSamples} samples at {condition.Scale} blocks per sample; sample it more coarsely or make it smaller.";
    }

    protected override MatchStatus Evaluate(BiomeCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var blocks = condition.Area.ToBlocks(reference);
        var grid = GetSampleGrid(condition, blocks);
        var samples = context.GetBytes(grid.SampleCount);
        context.GetGenerator(condition.Dimension).GenerateBiomes(grid, samples);

        var found = FindBiomes(condition, reference, blocks, grid, samples, out var middle);
        var holds = condition.Match == BiomeMatch.All ? found == condition.Biomes : !found.IsEmpty;
        if (holds)
            matches.Add(middle);
        return Decide(condition, reference, matches);
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

            var position = GetSamplePosition(grid, index, blocks);
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

    private static BiomeArea GetSampleGrid(BiomeCondition condition, BlockArea blocks)
    {
        var (left, top) = (FloorDivide(blocks.X, condition.Scale), FloorDivide(blocks.Z, condition.Scale));
        var (right, bottom) = (FloorDivide(blocks.Right - 1, condition.Scale), FloorDivide(blocks.Bottom - 1, condition.Scale));
        var y = condition.Scale == 1 ? condition.Y : condition.Y >> VerticalCellShift;
        return new BiomeArea(condition.Scale, left, top, right - left + 1, bottom - top + 1, y);
    }

    /// <summary>The middle of the part of a sample that lies in the rectangle of the area.</summary>
    private static BlockPos GetSamplePosition(BiomeArea grid, int index, BlockArea blocks)
    {
        var (x, z) = (grid.X + index % grid.SizeX, grid.Z + index / grid.SizeX);
        return new BlockPos(
            Math.Clamp(x * grid.Scale + grid.Scale / 2, blocks.X, blocks.Right - 1),
            Math.Clamp(z * grid.Scale + grid.Scale / 2, blocks.Z, blocks.Bottom - 1));
    }

    private static long CountSamplesAtMost(BiomeCondition condition)
        => ((long)condition.Area.SizeX / condition.Scale + 2) * ((long)condition.Area.SizeZ / condition.Scale + 2);

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
