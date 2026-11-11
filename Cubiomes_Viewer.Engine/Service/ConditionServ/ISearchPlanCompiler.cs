using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public interface ISearchPlanCompiler
{
    /// <summary>Arranges the conditions of a valid definition for testing seeds. A plan serves one worker.</summary>
    /// <exception cref="ArgumentException">A condition is relative to one that does not exist, or to itself through others.</exception>
    SearchPlan Compile(SearchDefinition definition);
}
