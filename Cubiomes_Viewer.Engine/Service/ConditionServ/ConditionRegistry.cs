using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public sealed class ConditionRegistry : IConditionRegistry
{
    public IReadOnlyList<ConditionDescriptor> Descriptors { get; }

    private readonly Dictionary<Type, IConditionEvaluator> _evaluators;

    public ConditionRegistry(IEnumerable<IConditionEvaluator> evaluators)
    {
        ArgumentNullException.ThrowIfNull(evaluators);

        _evaluators = evaluators.ToDictionary(evaluator => evaluator.Descriptor.ConditionType);
        Descriptors = [.. _evaluators.Values.Select(evaluator => evaluator.Descriptor)];
    }

    public IConditionEvaluator GetEvaluator(SearchCondition condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return _evaluators.TryGetValue(condition.GetType(), out var evaluator)
            ? evaluator
            : throw new NotSupportedException($"No evaluator is registered for conditions of type {condition.GetType().Name}.");
    }

    public ConditionDescriptor GetDescriptor(SearchCondition condition) => GetEvaluator(condition).Descriptor;
}
