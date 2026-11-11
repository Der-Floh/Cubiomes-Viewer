using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public interface IConditionValidator
{
    /// <summary>Lists what keeps a definition from being searched; nothing when it can be.</summary>
    IReadOnlyList<ConditionProblem> Validate(SearchDefinition definition);
}
