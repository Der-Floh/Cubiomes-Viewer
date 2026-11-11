using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public sealed class ConditionValidator : IConditionValidator
{
    private const int NoCondition = 0;

    private readonly IConditionRegistry _registry;
    private readonly IVersionCatalog _versionCatalog;

    public ConditionValidator(IConditionRegistry registry, IVersionCatalog versionCatalog)
    {
        _registry = registry;
        _versionCatalog = versionCatalog;
    }

    public IReadOnlyList<ConditionProblem> Validate(SearchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!_versionCatalog.Versions.Contains(definition.Version))
            return [new ConditionProblem(NoCondition, "Choose a Minecraft version.")];
        if (definition.Conditions.Count == 0)
            return [new ConditionProblem(NoCondition, "Add at least one condition.")];

        return [.. definition.Conditions.SelectMany(condition => GetProblems(condition, definition), (condition, message) => new ConditionProblem(condition.Id, message))];
    }

    private IEnumerable<string> GetProblems(SearchCondition condition, SearchDefinition definition)
    {
        var descriptor = _registry.GetDescriptor(condition);
        var structural = GetNumberProblems(condition, definition)
            .Concat(GetReferenceProblems(condition, definition))
            .Concat(GetAreaProblems(condition, descriptor))
            .Concat(GetCountProblems(condition, descriptor))
            .ToList();

        return structural.Count > 0 ? structural : _registry.GetEvaluator(condition).Validate(condition, definition);
    }

    private static IEnumerable<string> GetNumberProblems(SearchCondition condition, SearchDefinition definition)
    {
        if (condition.Id is < SearchCondition.MinId or > SearchCondition.MaxId)
            yield return $"Its number must be between {SearchCondition.MinId} and {SearchCondition.MaxId}.";
        if (definition.Conditions.Count(other => other.Id == condition.Id) > 1)
            yield return "Another condition has the same number.";
    }

    private static IEnumerable<string> GetReferenceProblems(SearchCondition condition, SearchDefinition definition)
    {
        if (condition.RelativeTo is not { } parent)
            yield break;

        if (parent == condition.Id)
            yield return "It cannot be relative to itself.";
        else if (definition.Conditions.All(other => other.Id != parent))
            yield return $"It is relative to condition {parent}, which does not exist.";
        else if (!ReachesRoot(condition, definition))
            yield return "It is relative to itself through other conditions.";
    }

    private static bool ReachesRoot(SearchCondition condition, SearchDefinition definition)
    {
        var current = condition;
        for (var step = 0; step <= definition.Conditions.Count; step++)
        {
            if (current.RelativeTo is not { } parent)
                return true;

            var next = definition.Conditions.FirstOrDefault(other => other.Id == parent);
            if (next is null)
                return true;
            current = next;
        }

        return false;
    }

    private static IEnumerable<string> GetAreaProblems(SearchCondition condition, ConditionDescriptor descriptor)
    {
        if (!descriptor.HasArea)
            yield break;

        var area = condition.Area;
        if (area.MinX > area.MaxX || area.MinZ > area.MaxZ || area.Radius < 0)
            yield return "The area has no size.";
        else if (Math.Min(area.MinX, area.MinZ) < -SearchArea.MaxOffset || Math.Max(area.MaxX, area.MaxZ) > SearchArea.MaxOffset)
            yield return $"The area may reach at most {SearchArea.MaxOffset.ToString("N0", CultureInfo.InvariantCulture)} blocks from its reference point.";
    }

    private static IEnumerable<string> GetCountProblems(SearchCondition condition, ConditionDescriptor descriptor)
    {
        if (condition.Count < 0)
            yield return "The number of matches cannot be negative.";
        else if (condition.IsExclusion && !descriptor.CanExclude)
            yield return "This kind of condition cannot ask for no match.";
        else if (condition.Count > descriptor.MaxCount)
            yield return $"At most {descriptor.MaxCount.ToString(CultureInfo.InvariantCulture)} matches can be asked for.";

        if (!Enum.IsDefined(condition.Anchor))
            yield return "Choose where the relative conditions start from.";
    }
}
