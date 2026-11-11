using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>What the user and the search need to know about one kind of condition.</summary>
public sealed record ConditionDescriptor
{
    public required Type ConditionType { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }

    /// <summary>Creates a condition of the kind with its defaults.</summary>
    public required Func<int, SearchCondition> Create { get; init; }

    /// <summary>How costly the kind is to test compared with the others; cheaper conditions are tested first.</summary>
    public required int Cost { get; init; }

    /// <summary>Whether the kind looks in an area of its own.</summary>
    public bool HasArea { get; init; } = true;

    /// <summary>The most matches the kind can be asked for; 1 when it is either there or not.</summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>Whether the kind can be turned around to ask for no match at all.</summary>
    public bool CanExclude { get; init; } = true;

    /// <summary>Whether the kind holds when one of the conditions relative to it holds, instead of all of them.</summary>
    public bool AcceptsAnyChild { get; init; }
}
