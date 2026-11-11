using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>One condition of a plan with its evaluator and the conditions relative to it, cheapest first.</summary>
public sealed class PlanNode
{
    public SearchCondition Condition { get; }
    public IConditionEvaluator Evaluator { get; }
    public IReadOnlyList<PlanNode> Children { get; }

    /// <summary>How costly the node is to test compared with its siblings.</summary>
    public int Cost { get; }

    public bool AcceptsAnyChild => Evaluator.Descriptor.AcceptsAnyChild;

    /// <summary>Where the condition was found the last time it was tested.</summary>
    internal List<BlockPos> Matches { get; } = [];

    public PlanNode(SearchCondition condition, IConditionEvaluator evaluator, IReadOnlyList<PlanNode> children)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(children);

        Condition = condition;
        Evaluator = evaluator;
        Children = children;
        Cost = evaluator.Descriptor.Cost + (AcceptsAnyChild && children.Count > 0 ? children.Min(child => child.Cost) : 0);
    }
}
