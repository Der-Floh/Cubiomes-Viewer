namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What one pass found out about one condition.</summary>
public enum MatchStatus
{
    /// <summary>The condition does not hold.</summary>
    Failed,

    /// <summary>The pass cannot tell, and it does not know where matches would be.</summary>
    Unknown,

    /// <summary>The pass cannot tell, but every match there can be is among the places it listed.</summary>
    Candidates,

    /// <summary>The condition holds at exactly the places listed.</summary>
    Matched,
}
