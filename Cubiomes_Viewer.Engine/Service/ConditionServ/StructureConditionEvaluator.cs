using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Structures of one type in an area. Where structures attempt to generate follows from the seed without its biomes,
/// so the quick pass already knows every place a structure can be at; only the biomes decide which of them are.
/// </summary>
public sealed class StructureConditionEvaluator : ConditionEvaluator<StructureCondition>
{
    private const int MaxRegions = 65536;
    private const int MaxMatches = 100;
    private const int SearchCost = 20;

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(StructureCondition),
        Name = "Structure",
        Description = "Structures of one type generate in the area.",
        Create = id => new StructureCondition { Id = id, Type = StructureType.Village },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

    private readonly IStructureFinder _structureFinder;
    private readonly IStructureCatalog _structureCatalog;

    public StructureConditionEvaluator(IStructureFinder structureFinder, IStructureCatalog structureCatalog)
    {
        _structureFinder = structureFinder;
        _structureCatalog = structureCatalog;
    }

    protected override IEnumerable<string> Validate(StructureCondition condition, SearchDefinition definition)
    {
        if (_structureCatalog.Structures.All(structure => structure.Type != condition.Type))
        {
            yield return $"The structure type {condition.Type} cannot be searched for.";
            yield break;
        }

        var name = _structureCatalog.GetInfo(condition.Type).Name;
        if (!_structureFinder.TryGetPlacement(condition.Type, definition.Version, out var placement))
            yield return $"{name} does not generate in this version.";
        else if (CountRegionsAtMost(condition.Area, placement) > MaxRegions)
            yield return $"The area is too large for {name}: it may span at most {MaxRegions} regions of {placement.RegionBlocks} blocks.";
    }

    protected override MatchStatus Evaluate(StructureCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (!_structureFinder.TryGetPlacement(condition.Type, context.Version, out var placement))
            return Decide(condition, reference, matches);

        var area = condition.Area.ToBlocks(reference);
        var found = context.GetPositions(checked((int)placement.CountRegions(area)));
        var count = pass == SearchPass.Quick
            ? _structureFinder.FindAttempts(condition.Type, context.Version, context.Seed, area, found)
            : context.GetGenerator(placement.Dimension).FindStructures(condition.Type, area, found);

        foreach (var position in found[..count])
        {
            if (condition.Area.Contains(reference, position))
                matches.Add(position);
        }

        return pass == SearchPass.Quick ? DecideCandidates(condition, reference, matches) : Decide(condition, reference, matches);
    }

    /// <summary>The most regions the area can touch wherever its reference point lies.</summary>
    private static long CountRegionsAtMost(SearchArea area, StructurePlacement placement)
        => ((long)area.SizeX / placement.RegionBlocks + 2) * ((long)area.SizeZ / placement.RegionBlocks + 2);
}
