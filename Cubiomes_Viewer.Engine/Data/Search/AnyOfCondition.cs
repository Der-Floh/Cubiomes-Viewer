namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Holds when at least one of the conditions relative to it holds. It has no area of its own: those conditions
/// look around its own reference point.
/// </summary>
public sealed record AnyOfCondition : SearchCondition;
