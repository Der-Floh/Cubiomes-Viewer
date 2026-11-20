namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What bounds where a condition looks.</summary>
public enum SearchLimit
{
    /// <summary>The condition looks inside its area.</summary>
    Area = 0,

    /// <summary>
    /// The condition looks outward from its reference point, nearest first, until it has found as many matches as it asks
    /// for, as far as the world border.
    /// </summary>
    Findings,
}
