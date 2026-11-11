using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>What a search looks for: the conditions a seed has to meet in one Minecraft version. Two definitions with the same content are equal.</summary>
public sealed record SearchDefinition
{
    public McVersion Version { get; init; } = McVersion.V1_21_3;
    public bool LargeBiomes { get; init; }
    public IReadOnlyList<SearchCondition> Conditions { get; init; } = [];

    /// <summary>The generator flags of the worlds the search looks at; the same ones the map uses, so a found seed looks as it was found.</summary>
    [JsonIgnore]
    public GeneratorFlags Flags => GeneratorFlags.ForceOceanVariants | (LargeBiomes ? GeneratorFlags.LargeBiomes : GeneratorFlags.None);

    public bool Equals(SearchDefinition? other)
        => other is not null && Version == other.Version && LargeBiomes == other.LargeBiomes && Conditions.SequenceEqual(other.Conditions);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Version);
        hash.Add(LargeBiomes);
        foreach (var condition in Conditions)
            hash.Add(condition);
        return hash.ToHashCode();
    }
}
