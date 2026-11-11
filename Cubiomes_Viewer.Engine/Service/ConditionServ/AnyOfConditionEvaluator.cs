using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>Stands for "one of these": it is found at its reference point, and one condition relative to it is enough.</summary>
public sealed class AnyOfConditionEvaluator : ConditionEvaluator<AnyOfCondition>
{
    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(AnyOfCondition),
        Name = "Any of",
        Description = "At least one of the conditions relative to this one holds.",
        Create = id => new AnyOfCondition { Id = id },
        Cost = 0,
        HasArea = false,
        CanExclude = false,
        AcceptsAnyChild = true,
    };

    protected override IEnumerable<string> Validate(AnyOfCondition condition, SearchDefinition definition)
    {
        if (definition.Conditions.All(other => other.RelativeTo != condition.Id))
            yield return "Add at least one condition relative to this one.";
    }

    protected override MatchStatus Evaluate(AnyOfCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        matches.Add(reference);
        return MatchStatus.Matched;
    }
}
