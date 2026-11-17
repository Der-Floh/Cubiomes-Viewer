using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Strongholds lie in the area, with at least <see cref="PortalEyes"/> eyes of ender already in their end portal.</summary>
public sealed record StrongholdCondition : SearchCondition
{
    /// <summary>The frames around an end portal, each of which can hold an eye of ender.</summary>
    public const int PortalFrames = 12;

    /// <summary>The fewest eyes a stronghold's end portal has to hold already: 0 for any stronghold, 12 for one whose portal is open.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PortalEyes { get; init; }

    /// <summary>An item the containers of the strongholds have to hold.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LootRequirement? Loot { get; init; }
}
