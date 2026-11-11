using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public interface IConditionRegistry
{
    /// <summary>The kinds of conditions there are, in the order they are offered.</summary>
    IReadOnlyList<ConditionDescriptor> Descriptors { get; }

    IConditionEvaluator GetEvaluator(SearchCondition condition);

    ConditionDescriptor GetDescriptor(SearchCondition condition);
}
