using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

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
    private readonly ILootCatalog _lootCatalog;

    public StructureConditionEvaluator(IStructureFinder structureFinder, IStructureCatalog structureCatalog, ILootCatalog lootCatalog)
    {
        _structureFinder = structureFinder;
        _structureCatalog = structureCatalog;
        _lootCatalog = lootCatalog;
    }

    protected override Dimension GetDimension(StructureCondition condition, McVersion version)
        => _structureFinder.TryGetPlacement(condition.Type, version, out var placement) ? placement.Dimension : Dimension.Overworld;

    protected override IEnumerable<string> Validate(StructureCondition condition, SearchDefinition definition)
    {
        if (_structureCatalog.Structures.All(structure => structure.Type != condition.Type))
        {
            yield return $"The structure type {condition.Type} cannot be searched for.";
            yield break;
        }

        var name = _structureCatalog.GetInfo(condition.Type).Name;
        if (!Enum.IsDefined(condition.Trait) || !condition.Trait.AppliesTo(condition.Type))
            yield return $"{name} has no such kind.";
        else if (condition.Trait == StructureTrait.DriedGhast && definition.Version < McVersion.V1_21_6)
            yield return "Nether fossils hold dried ghasts from 1.21.6 on.";

        if (!_structureFinder.TryGetPlacement(condition.Type, definition.Version, out var placement))
            yield return $"{name} does not generate in this version.";
        else if (CountRegionsAtMost(condition.Area, placement) > MaxRegions)
            yield return $"The area is too large for {name}: it may span at most {MaxRegions} regions of {placement.RegionBlocks} blocks.";

        if (condition.Loot is { } loot)
        {
            foreach (var problem in LootProblems.Find(loot, condition.Type, name, definition.Version, _lootCatalog))
                yield return problem;
        }
    }

    protected override MatchStatus Evaluate(StructureCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (!_structureFinder.TryGetPlacement(condition.Type, context.Version, out var placement))
            return Decide(condition, reference, matches);

        var area = condition.Area.ToBlocks(reference);
        var found = context.GetPositions(checked((int)placement.CountRegions(area)));
        var generator = pass == SearchPass.Quick ? null : context.GetGenerator(placement.Dimension);
        var count = generator is null
            ? _structureFinder.FindAttempts(condition.Type, context.Version, context.Rules, context.Seed, area, found)
            : generator.FindStructures(condition.Type, area, found);

        foreach (var position in found[..count])
        {
            if (!condition.Area.Contains(reference, position))
                continue;
            if (generator is null || IsMatch(condition, generator, position))
                matches.Add(position);
        }

        return pass == SearchPass.Quick ? DecideCandidates(condition, reference, matches) : Decide(condition, reference, matches);
    }

    private static bool IsMatch(StructureCondition condition, IWorldGenerator generator, BlockPos position)
        => (condition.Trait == StructureTrait.None || condition.Trait.IsIn(generator.DescribeStructure(condition.Type, position)))
            && (condition.Loot is not { } loot || LootMatcher.Holds(generator, condition.Type, position, loot));

    /// <summary>The most regions the area can touch wherever its reference point lies.</summary>
    private static long CountRegionsAtMost(SearchArea area, StructurePlacement placement)
        => ((long)area.SizeX / placement.RegionBlocks + 2) * ((long)area.SizeZ / placement.RegionBlocks + 2);
}
