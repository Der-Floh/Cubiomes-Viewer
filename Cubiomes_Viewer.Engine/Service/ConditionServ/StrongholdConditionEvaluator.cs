using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>Strongholds in an area. They move to suitable biomes, so the quick pass cannot tell.</summary>
public sealed class StrongholdConditionEvaluator : ConditionEvaluator<StrongholdCondition>
{
    /// <summary>
    /// The closest a stronghold of the innermost ring comes to the world origin since 1.9: the ring starts 1408 blocks
    /// out, and looking for a biome and settling in a chunk move a stronghold by less than 256 blocks.
    /// </summary>
    public const int NearestRingBlocks = 1152;

    /// <summary>How much further out each following ring lies.</summary>
    public const int RingSpacingBlocks = 3072;

    private const McVersion FirstVersion = McVersion.Beta1_8;
    private const McVersion FirstRingVersion = McVersion.V1_9_4;
    private const int StrongholdsBeforeRings = 3;
    private const int MaxMatches = 128;
    private const int DefaultRadius = 3072;
    private const int SearchCost = 40;
    private static readonly int[] _strongholdsUpToRing = [3, 9, 19, 34, 55, 83, 119, 128];

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(StrongholdCondition),
        Name = "Stronghold",
        Description = "Strongholds lie in the area.",
        Create = id => new StrongholdCondition { Id = id, Area = SearchArea.Circle(DefaultRadius) },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

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
            yield return "This version has no strongholds.";
    }

    protected override MatchStatus Evaluate(StrongholdCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var strongholds = context.GetPositions(CountStrongholdsWithin(context.Version, GetReach(condition.Area, reference)));
        var count = strongholds.Length == 0 ? 0 : context.GetGenerator(Dimension.Overworld).FindStrongholds(strongholds);
        foreach (var stronghold in strongholds[..count])
        {
            if (condition.Area.Contains(reference, stronghold))
                matches.Add(stronghold);
        }

        return Decide(condition, reference, matches);
    }

    /// <summary>The furthest the area reaches from the world origin.</summary>
    private static double GetReach(SearchArea area, BlockPos reference)
    {
        var x = Math.Max(Math.Abs((long)reference.X + area.MinX), Math.Abs((long)reference.X + area.MaxX));
        var z = Math.Max(Math.Abs((long)reference.Z + area.MinZ), Math.Abs((long)reference.Z + area.MaxZ));
        var corner = double.Hypot(x, z);
        return area.Radius > 0 ? Math.Min(corner, double.Hypot(reference.X, reference.Z) + area.Radius) : corner;
    }
}
