using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// One requirement a seed has to meet: something that must be found, or must not be found, in an area.
/// Every kind of condition is listed here so that a definition can be stored and sent to a worker.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(StructureCondition), "structure")]
[JsonDerivedType(typeof(BiomeCondition), "biome")]
[JsonDerivedType(typeof(SpawnCondition), "spawn")]
[JsonDerivedType(typeof(StrongholdCondition), "stronghold")]
[JsonDerivedType(typeof(SlimeChunkCondition), "slimeChunks")]
[JsonDerivedType(typeof(AnyOfCondition), "anyOf")]
[JsonDerivedType(typeof(QuadHutCondition), "quadHuts")]
[JsonDerivedType(typeof(ClimateCondition), "climate")]
[JsonDerivedType(typeof(BiomePatchCondition), "biomePatches")]
[JsonDerivedType(typeof(TemperatureCategoryCondition), "temperatureCategory")]
public abstract record SearchCondition
{
    public const int MinId = 1;
    public const int MaxId = 99;
    public const int DefaultSide = 1024;

    /// <summary>Identifies the condition within its definition, from <see cref="MinId"/> to <see cref="MaxId"/>.</summary>
    public required int Id { get; init; }

    /// <summary>
    /// The id of the condition whose match is the reference point of this one, or <see langword="null"/> to look
    /// around the world origin.
    /// </summary>
    public int? RelativeTo { get; init; }

    public SearchArea Area { get; init; } = SearchArea.Square(DefaultSide);

    /// <summary>How many matches the area must hold at least. Zero turns the condition around: the area must hold none.</summary>
    public int Count { get; init; } = 1;

    /// <summary>Where the conditions relative to this one start from when it has several matches.</summary>
    public MatchAnchor Anchor { get; init; } = MatchAnchor.AnyMatch;

    [JsonIgnore]
    public bool IsExclusion => Count == 0;
}
