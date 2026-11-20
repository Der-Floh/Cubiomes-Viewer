using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Strongholds in an area, optionally with eyes of ender already in their end portal or an item in their chests. They move to
/// suitable biomes, so the quick pass cannot tell.
/// </summary>
public sealed class StrongholdConditionEvaluator : ConditionEvaluator<StrongholdCondition>
{
    /// <summary>
    /// The closest a stronghold of the innermost ring comes to the world origin since 1.9: the ring starts 1408 blocks
    /// out, and looking for a biome and settling in a chunk move a stronghold by less than 256 blocks.
    /// </summary>
    public const int NearestRingBlocks = 1152;

    /// <summary>How much further out each following ring lies.</summary>
    public const int RingSpacingBlocks = 3072;

    private const string StrongholdName = "Stronghold";
    private const McVersion FirstVersion = McVersion.Beta1_8;
    private const McVersion FirstRingVersion = McVersion.V1_9_4;
    private const McVersion FirstPortalEyesVersion = McVersion.V1_13_2;
    private const int StrongholdsBeforeRings = 3;
    private const int MaxMatches = 128;
    private const int DefaultRadius = 3072;
    private const int SearchCost = 40;
    private static readonly int[] _strongholdsUpToRing = [3, 9, 19, 34, 55, 83, 119, 128];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(StrongholdCondition),
        Name = StrongholdName,
        Description = "Strongholds lie in the area, optionally with eyes of ender already in their end portal.",
        Create = id => new StrongholdCondition { Id = id, Area = SearchArea.Circle(DefaultRadius) },
        Cost = SearchCost,
        MaxCount = MaxMatches,
        CanLimitFindings = true,
    };

    private readonly ILootCatalog _lootCatalog;

    public StrongholdConditionEvaluator(ILootCatalog lootCatalog)
    {
        _lootCatalog = lootCatalog;
    }

    /// <summary>How many strongholds, in generation order, can lie within a distance of the world origin.</summary>
    public static int CountStrongholdsWithin(McVersion version, double blocks)
    {
        if (version < FirstRingVersion)
            return StrongholdsBeforeRings;
        if (blocks < NearestRingBlocks)
            return 0;

        var ring = Math.Min((int)((blocks - NearestRingBlocks) / RingSpacingBlocks), _strongholdsUpToRing.Length - 1);
        return _strongholdsUpToRing[ring];
    }

    protected override IEnumerable<string> Validate(StrongholdCondition condition, SearchDefinition definition)
    {
        if (definition.Version < FirstVersion)
        {
            yield return "This version has no strongholds.";
            yield break;
        }

        if (condition.PortalEyes is < 0 or > StrongholdCondition.PortalFrames)
            yield return "An end portal has 12 frames for eyes of ender.";
        else if (condition.PortalEyes > 0 && definition.Version < FirstPortalEyesVersion)
            yield return "The eyes in a stronghold's end portal are known from 1.13 on.";

        if (condition.Loot is { } loot)
        {
            foreach (var problem in LootProblems.Find(loot, StructureType.Stronghold, StrongholdName, definition.Version, _lootCatalog))
                yield return problem;
        }
    }

    protected override MatchStatus Evaluate(StrongholdCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;
        if (condition.Limit == SearchLimit.Findings)
            return FindNearest(condition, reference, context, matches);

        var strongholds = context.GetPositions(CountStrongholdsWithin(context.Version, GetReach(condition.Area, reference)));
        var generator = context.GetGenerator(Dimension.Overworld);
        var count = strongholds.Length == 0 ? 0 : generator.FindStrongholds(strongholds);
        foreach (var stronghold in strongholds[..count])
        {
            if (condition.Area.Contains(reference, stronghold) && IsMatch(condition, generator, stronghold))
                matches.Add(stronghold);
        }

        return Decide(condition, reference, matches);
    }

    /// <summary>Tests the strongholds of the whole world, nearest first, until it has found the matches asked for.</summary>
    private static MatchStatus FindNearest(StrongholdCondition condition, BlockPos reference, SearchSeedContext context, List<BlockPos> matches)
    {
        var strongholds = context.GetPositions(CountAllStrongholds(context.Version));
        var generator = context.GetGenerator(Dimension.Overworld);
        var count = generator.FindStrongholds(strongholds);
        var nearestFirst = strongholds[..count].ToArray().OrderBy(position => position.SquaredDistanceTo(reference)).ThenBy(position => position.Z).ThenBy(position => position.X);
        foreach (var stronghold in nearestFirst)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!IsMatch(condition, generator, stronghold))
                continue;

            matches.Add(stronghold);
            if (matches.Count == condition.Count)
                break;
        }

        return Decide(condition, reference, matches);
    }

    private static int CountAllStrongholds(McVersion version) => version < FirstRingVersion ? StrongholdsBeforeRings : _strongholdsUpToRing[^1];

    private static bool IsMatch(StrongholdCondition condition, IWorldGenerator generator, BlockPos stronghold)
        => HasPortalEyes(generator, stronghold, condition.PortalEyes)
            && (condition.Loot is not { } loot || LootMatcher.Holds(generator, StructureType.Stronghold, stronghold, loot));

    private static bool HasPortalEyes(IWorldGenerator generator, BlockPos stronghold, int eyes)
        => eyes == 0 || generator.DescribeStructure(StructureType.Stronghold, stronghold).Variant?.PortalEyes >= eyes;

    /// <summary>The furthest the area reaches from the world origin.</summary>
    private static double GetReach(SearchArea area, BlockPos reference)
    {
        var x = Math.Max(Math.Abs((long)reference.X + area.MinX), Math.Abs((long)reference.X + area.MaxX));
        var z = Math.Max(Math.Abs((long)reference.Z + area.MinZ), Math.Abs((long)reference.Z + area.MaxZ));
        var corner = double.Hypot(x, z);
        return area.Radius > 0 ? Math.Min(corner, double.Hypot(reference.X, reference.Z) + area.Radius) : corner;
    }
}
