using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Patches of biomes in an area: neighbouring samples on a grid with one of the biomes, filled outwards from each
/// sample. Biomes need the whole seed, so the quick pass cannot tell.
/// </summary>
public sealed class BiomePatchConditionEvaluator : ConditionEvaluator<BiomePatchCondition>
{
    private const int MaxSamples = 512 * 512;
    private const int MaxMatches = 64;
    private const int SearchCost = 55;
    private const int DefaultSide = 4096;
    private const int BlocksPerChunk = 16 * 16;
    private static readonly int[] _scales = [4, 16, 64, 256];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(BiomePatchCondition),
        Name = "Biome patch",
        Description = "Patches of biomes of a size lie in the area.",
        Create = id => new BiomePatchCondition { Id = id, Biomes = BiomeSet.Empty, Area = SearchArea.Square(DefaultSide) },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

    private readonly IBiomeCatalog _biomeCatalog;

    public BiomePatchConditionEvaluator(IBiomeCatalog biomeCatalog)
    {
        _biomeCatalog = biomeCatalog;
    }

    /// <summary>How many samples a patch needs at least to cover its chunks.</summary>
    public static int CountSamplesNeeded(BiomePatchCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return (int)Math.Max(1, Math.Ceiling((double)condition.MinChunks * BlocksPerChunk / ((long)condition.Scale * condition.Scale)));
    }

    protected override IEnumerable<string> Validate(BiomePatchCondition condition, SearchDefinition definition)
    {
        foreach (var problem in BiomeProblems.Find(condition.Biomes, condition.Dimension, definition.Version, _biomeCatalog))
            yield return problem;

        if (condition.MinChunks < 1)
            yield return "A patch covers at least one chunk.";

        foreach (var problem in SampleGrid.FindProblems(condition.Area, condition.Scale, _scales, MaxSamples))
            yield return problem;
    }

    protected override MatchStatus Evaluate(BiomePatchCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var blocks = condition.Area.ToBlocks(reference);
        var grid = SampleGrid.Create(blocks, condition.Scale, condition.Y);
        var samples = context.GetBytes(grid.SampleCount);
        context.GetGenerator(condition.Dimension).GenerateBiomes(grid, samples);

        var finder = new PatchFinder(condition, reference, blocks, grid, samples, context.GetMarks(samples.Length), context.GetIndices(samples.Length));
        finder.FindAll(CountSamplesNeeded(condition), MaxMatches, matches);
        return Decide(condition, reference, matches);
    }

    /// <summary>Finds the patches of one grid of samples, each sample at most once.</summary>
    private ref struct PatchFinder(BiomePatchCondition condition, BlockPos reference, BlockArea blocks, BiomeArea grid, ReadOnlySpan<byte> samples, Span<bool> seen, Span<int> pending)
    {
        private readonly ReadOnlySpan<byte> _samples = samples;
        private readonly Span<bool> _seen = seen;
        private readonly Span<int> _pending = pending;
        private int _top;

        /// <summary>Adds the middle of every patch with enough samples, in the order their first samples come.</summary>
        public void FindAll(int samplesNeeded, int maxMatches, List<BlockPos> matches)
        {
            for (var start = 0; start < _samples.Length && matches.Count < maxMatches; start++)
            {
                if (_seen[start] || !IsWanted(start))
                    continue;

                var (count, middle) = Fill(start);
                if (count >= samplesNeeded)
                    matches.Add(middle);
            }
        }

        private (int Count, BlockPos Middle) Fill(int start)
        {
            long sumX = 0, sumZ = 0;
            var count = 0;
            Push(start);

            while (_top > 0)
            {
                var index = _pending[--_top];
                var position = SampleGrid.GetPosition(grid, index, blocks);
                sumX += position.X;
                sumZ += position.Z;
                count++;

                var (x, z) = (index % grid.SizeX, index / grid.SizeX);
                if (x > 0)
                    TryPush(index - 1);
                if (x < grid.SizeX - 1)
                    TryPush(index + 1);
                if (z > 0)
                    TryPush(index - grid.SizeX);
                if (z < grid.SizeZ - 1)
                    TryPush(index + grid.SizeX);
            }

            return (count, new BlockPos((int)(sumX / count), (int)(sumZ / count)));
        }

        private void TryPush(int index)
        {
            if (!_seen[index] && IsWanted(index))
                Push(index);
        }

        private void Push(int index)
        {
            _seen[index] = true;
            _pending[_top++] = index;
        }

        private readonly bool IsWanted(int index)
            => condition.Biomes.Contains(_samples[index]) && condition.Area.Contains(reference, SampleGrid.GetPosition(grid, index, blocks));
    }
}
