using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public sealed class SearchPlanCompiler : ISearchPlanCompiler
{
    private readonly IConditionRegistry _registry;

    public SearchPlanCompiler(IConditionRegistry registry)
    {
        _registry = registry;
    }

    public SearchPlan Compile(SearchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var ids = definition.Conditions.Select(condition => condition.Id).ToHashSet();
        if (ids.Count != definition.Conditions.Count)
            throw new ArgumentException("Two conditions have the same number.", nameof(definition));

        var orphan = definition.Conditions.FirstOrDefault(condition => condition.RelativeTo is { } parent && !ids.Contains(parent));
        if (orphan is not null)
            throw new ArgumentException($"Condition {orphan.Id} is relative to condition {orphan.RelativeTo}, which does not exist.", nameof(definition));

        var childrenByParent = definition.Conditions.ToLookup(condition => condition.RelativeTo);
        var reached = 0;
        var roots = Build(null);
        if (reached != definition.Conditions.Count)
            throw new ArgumentException("Some conditions are relative to themselves through other conditions.", nameof(definition));

        return new SearchPlan(roots);

        PlanNode[] Build(int? parent)
            => [.. childrenByParent[parent].Select(CreateNode).OrderBy(node => node.Cost)];

        PlanNode CreateNode(SearchCondition condition)
        {
            reached++;
            return new PlanNode(condition, _registry.GetEvaluator(condition), Build(condition.Id));
        }
    }
}
