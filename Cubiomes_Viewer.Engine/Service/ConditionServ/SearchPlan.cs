namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>The conditions of a definition as trees: each root looks around the world origin, the others around a match of their parent.</summary>
public sealed class SearchPlan
{
    public IReadOnlyList<PlanNode> Roots { get; }

    public SearchPlan(IReadOnlyList<PlanNode> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        Roots = roots;
    }
}
