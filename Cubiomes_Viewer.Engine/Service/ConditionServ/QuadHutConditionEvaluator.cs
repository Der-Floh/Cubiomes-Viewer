using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Quads of huts in an area. Where the four huts attempt to generate follows from the seed without its biomes, so the
/// quick pass finds every quad there can be; the full pass keeps those whose four huts all can generate.
/// </summary>
public sealed class QuadHutConditionEvaluator : ConditionEvaluator<QuadHutCondition>
{
    private const int MaxRegions = 65536;
    private const int MaxMatches = 16;
    private const int DefaultSide = 2048;
    private const int SearchCost = 15;
    private static readonly QuadQuality[] _qualities = [QuadQuality.Ideal, QuadQuality.Classic, QuadQuality.Normal, QuadQuality.Barely];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(QuadHutCondition),
        Name = "Quad huts",
        Description = "Four huts that generate in range of one point have their middle in the area.",
        Create = id => new QuadHutCondition { Id = id, Area = SearchArea.Square(DefaultSide) },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

    private readonly IQuadFinder _quadFinder;
    private readonly IStructureFinder _structureFinder;
    private readonly IStructureCatalog _structureCatalog;

    public QuadHutConditionEvaluator(IQuadFinder quadFinder, IStructureFinder structureFinder, IStructureCatalog structureCatalog)
    {
        _quadFinder = quadFinder;
        _structureFinder = structureFinder;
        _structureCatalog = structureCatalog;
    }

    protected override IEnumerable<string> Validate(QuadHutCondition condition, SearchDefinition definition)
    {
        if (!_qualities.Contains(condition.Quality))
            yield return "Choose how close the four huts have to be.";

        if (!Enum.IsDefined(condition.Type) || !_quadFinder.SupportsQuads(condition.Type, definition.Version) || !_structureFinder.TryGetPlacement(condition.Type, definition.Version, out var placement))
        {
            yield return $"{DescribeType(condition.Type)} form no quads in this version.";
            yield break;
        }

        if (CountRegionsAtMost(condition.Area, placement) > MaxRegions)
            yield return $"The area is too large: it may span at most {MaxRegions} regions of {placement.RegionBlocks} blocks.";
    }

    protected override MatchStatus Evaluate(QuadHutCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (!_structureFinder.TryGetPlacement(condition.Type, context.Version, out var placement) || !_quadFinder.SupportsQuads(condition.Type, context.Version))
            return Decide(condition, reference, matches);

        var size = placement.RegionBlocks;
        var blocks = condition.Area.ToBlocks(reference);
        var (x0, z0) = (FloorDivide(blocks.X, size) - 1, FloorDivide(blocks.Z, size) - 1);
        var (x1, z1) = (FloorDivide(blocks.Right - 1, size), FloorDivide(blocks.Bottom - 1, size));
        var quads = context.GetQuads((x1 - x0 + 1) * (z1 - z0 + 1));
        var count = _quadFinder.FindQuads(condition.Type, context.Version, context.Seed, x0, z0, x1, z1, quads);

        Span<BlockPos> huts = stackalloc BlockPos[StructureFinderExtensions.StructuresPerQuad];
        foreach (var quad in quads[..count])
        {
            if (quad.Quality > condition.Quality || !_structureFinder.TryGetQuadPositions(condition.Type, context.Version, context.Seed, quad, huts))
                continue;

            var middle = BlockPos.MiddleOf(huts);
            if (condition.Area.Contains(reference, middle) && (pass == SearchPass.Quick || CanAllGenerate(condition.Type, context, huts)))
                matches.Add(middle);
        }

        return pass == SearchPass.Quick ? DecideCandidates(condition, reference, matches) : Decide(condition, reference, matches);
    }

    private static bool CanAllGenerate(StructureType type, SearchSeedContext context, ReadOnlySpan<BlockPos> huts)
    {
        var generator = context.GetGenerator(Dimension.Overworld);
        foreach (var hut in huts)
        {
            if (!generator.CanGenerateStructure(type, hut))
                return false;
        }

        return true;
    }

    private string DescribeType(StructureType type)
        => _structureCatalog.Structures.FirstOrDefault(info => info.Type == type) is { } info ? $"{info.Name}s" : type.ToString();

    private static long CountRegionsAtMost(SearchArea area, StructurePlacement placement)
        => ((long)area.SizeX / placement.RegionBlocks + 3) * ((long)area.SizeZ / placement.RegionBlocks + 3);

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}
