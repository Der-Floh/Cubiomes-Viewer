using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What a search looks for: the conditions a seed has to meet in one Minecraft version. Two definitions with the same content are equal.</summary>
public sealed record SearchDefinition
{
    public McVersion Version { get; init; } = McVersion.V1_21_3;
    public bool LargeBiomes { get; init; }

    /// <summary>How structures generate in the worlds the search looks at.</summary>
    public StructureRules Rules
    {
        get;
        init => field = value ?? StructureRules.Default;
    } = StructureRules.Default;

    public IReadOnlyList<SearchCondition> Conditions { get; init; } = [];

    /// <summary>The generator flags of the worlds the search looks at; the same ones the map uses, so a found seed looks as it was found.</summary>
    [JsonIgnore]
    public GeneratorFlags Flags => GeneratorFlags.ForceOceanVariants | (LargeBiomes ? GeneratorFlags.LargeBiomes : GeneratorFlags.None);

    /// <summary>The world of a seed as the search looks at it.</summary>
    public WorldKey GetWorld(long seed, Dimension dimension) => new(seed, Version, dimension, Flags) { Rules = Rules };

    public bool Equals(SearchDefinition? other)
        => other is not null && Version == other.Version && LargeBiomes == other.LargeBiomes && Rules.Equals(other.Rules) && Conditions.SequenceEqual(other.Conditions);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version);
        hash.Add(LargeBiomes);
        hash.Add(Rules);
        foreach (var condition in Conditions)
            hash.Add(condition);
        return hash.ToHashCode();
    }
}
