namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Why a definition cannot be searched as it is. <see cref="ConditionId"/> is zero when the problem is not about one condition.</summary>
public sealed record ConditionProblem(int ConditionId, string Message)
{
    public override string ToString() => ConditionId == 0 ? Message : $"Condition {ConditionId}: {Message}";
}
