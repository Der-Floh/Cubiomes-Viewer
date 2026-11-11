namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Which place the conditions relative to a condition start from when it found several matches.</summary>
public enum MatchAnchor
{
    /// <summary>Every match is tried until the relative conditions hold at one of them.</summary>
    AnyMatch,

    /// <summary>Only the match closest to the reference point counts.</summary>
    Nearest,

    /// <summary>The middle of all matches.</summary>
    Center,
}
