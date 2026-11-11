using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>The world spawn point in an area. It follows from the biomes, so the quick pass cannot tell.</summary>
public sealed class SpawnConditionEvaluator : ConditionEvaluator<SpawnCondition>
{
    private const McVersion FirstPredictableVersion = McVersion.Beta1_8;
    private const int SearchCost = 30;

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(SpawnCondition),
        Name = "World spawn",
        Description = "The world spawn point lies in the area.",
        Create = id => new SpawnCondition { Id = id },
        Cost = SearchCost,
    };

    protected override IEnumerable<string> Validate(SpawnCondition condition, SearchDefinition definition)
    {
        if (definition.Version < FirstPredictableVersion)
            yield return "The spawn point of this version cannot be predicted.";
    }

    protected override MatchStatus Evaluate(SpawnCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        if (context.GetGenerator(Dimension.Overworld).FindSpawn() is { } spawn && condition.Area.Contains(reference, spawn))
            matches.Add(spawn);
        return Decide(condition, reference, matches);
    }
}
