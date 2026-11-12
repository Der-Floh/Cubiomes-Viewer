using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Which seeds a search scans and in what order. Every kind is listed here so that a setup can be stored.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$source")]
[JsonDerivedType(typeof(SeedRangeSource), "range")]
[JsonDerivedType(typeof(QuadHutSeedSource), "quadHuts")]
[JsonDerivedType(typeof(SeedListSource), "list")]
public abstract record SeedSource;

/// <summary>Every seed from <see cref="FirstSeed"/> to <see cref="LastSeed"/>, ascending.</summary>
public sealed record SeedRangeSource : SeedSource
{
    public long FirstSeed { get; init; }

    /// <summary>The last seed to scan; without it the search runs through all seeds from <see cref="FirstSeed"/> upwards.</summary>
    public long LastSeed { get; init; } = long.MaxValue;
}

/// <summary>
/// The seeds with a quad of huts in the regions from (<see cref="RegionX"/>, <see cref="RegionZ"/>) to the next region
/// in both directions: every quad-hut base of the class and every better one, each with all 65,536 values of the upper
/// 16 bits. The default regions put the quad around the world origin.
/// </summary>
public sealed record QuadHutSeedSource : SeedSource
{
    public const int AroundTheOrigin = -1;

    public StructureType Type { get; init; } = StructureType.SwampHut;
    public QuadQuality Quality { get; init; } = QuadQuality.Barely;
    public int RegionX { get; init; } = AroundTheOrigin;
    public int RegionZ { get; init; } = AroundTheOrigin;
}

/// <summary>The seeds of a list, in its order. Two lists with the same seeds are equal.</summary>
public sealed record SeedListSource : SeedSource
{
    public const int MaxSeeds = 1_000_000;

    public IReadOnlyList<long> Seeds { get; init; } = [];

    public bool Equals(SeedListSource? other) => other is not null && Seeds.SequenceEqual(other.Seeds);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var seed in Seeds)
            hash.Add(seed);
        return hash.ToHashCode();
    }
}
