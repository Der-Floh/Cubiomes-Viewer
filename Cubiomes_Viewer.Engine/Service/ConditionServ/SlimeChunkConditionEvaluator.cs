using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>Slime chunks in an area. They follow from the seed without its biomes, so every pass decides.</summary>
public sealed class SlimeChunkConditionEvaluator : ConditionEvaluator<SlimeChunkCondition>
{
    private const int ChunkBlocks = 16;
    private const int MaxChunksPerSide = 512;
    private const int MaxMatches = MaxChunksPerSide * MaxChunksPerSide;
    private const int DefaultSide = 9 * ChunkBlocks;
    private const int DefaultCount = 20;
    private const int SearchCost = 10;
    private const byte IsSlimeChunk = 1;

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(SlimeChunkCondition),
        Name = "Slime chunks",
        Description = "Slime chunks lie in the area.",
        Create = id => new SlimeChunkCondition { Id = id, Area = SearchArea.Square(DefaultSide), Count = DefaultCount },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

    private readonly ISlimeChunkFinder _slimeChunkFinder;

    public SlimeChunkConditionEvaluator(ISlimeChunkFinder slimeChunkFinder)
    {
        _slimeChunkFinder = slimeChunkFinder;
    }

    protected override IEnumerable<string> Validate(SlimeChunkCondition condition, SearchDefinition definition)
    {
        var maxBlocks = MaxChunksPerSide * ChunkBlocks;
        if (condition.Area.SizeX > maxBlocks || condition.Area.SizeZ > maxBlocks)
            yield return $"The area may be at most {maxBlocks} blocks wide and long.";
    }

    protected override MatchStatus Evaluate(SlimeChunkCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        var blocks = condition.Area.ToBlocks(reference);
        var (left, top) = (CeilingDivide(blocks.X), CeilingDivide(blocks.Z));
        var (sizeX, sizeZ) = (FloorDivide(blocks.Right - 1) - left + 1, FloorDivide(blocks.Bottom - 1) - top + 1);
        var count = sizeX > 0 && sizeZ > 0 ? CountSlimeChunks(condition, reference, context, left, top, sizeX, sizeZ) : 0;

        if (condition.IsExclusion ? count == 0 : count >= condition.Count)
        {
            matches.Add(condition.IsExclusion ? reference : GetAreaCenter(condition.Area, reference));
            return MatchStatus.Matched;
        }

        return MatchStatus.Failed;
    }

    private int CountSlimeChunks(SlimeChunkCondition condition, BlockPos reference, SearchSeedContext context, int left, int top, int sizeX, int sizeZ)
    {
        var chunks = context.GetBytes(sizeX * sizeZ);
        _slimeChunkFinder.FindSlimeChunks(context.Seed, left, top, sizeX, sizeZ, chunks);

        var count = 0;
        for (var index = 0; index < chunks.Length; index++)
        {
            var corner = new BlockPos((left + index % sizeX) * ChunkBlocks, (top + index / sizeX) * ChunkBlocks);
            if (chunks[index] == IsSlimeChunk && condition.Area.Contains(reference, corner))
                count++;
        }

        return count;
    }

    private static int FloorDivide(int block) => (int)Math.Floor(block / (double)ChunkBlocks);

    private static int CeilingDivide(int block) => (int)Math.Ceiling(block / (double)ChunkBlocks);
}
