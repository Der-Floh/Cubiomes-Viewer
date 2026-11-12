namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>A named set of biome colours; the biomes it leaves out keep their default colour.</summary>
public sealed record BiomeColorMap
{
    public const int MaxNameLength = 60;

    public required string Name { get; init; }

    /// <summary>The colours that replace the defaults, one per biome id at most and in the order of the ids.</summary>
    public IReadOnlyList<BiomeColor> Colors
    {
        get;
        init => field = [.. (value ?? []).GroupBy(color => color.Biome).Select(group => group.Last()).OrderBy(color => color.Biome)];
    } = [];

    public bool Equals(BiomeColorMap? other)
        => other is not null && string.Equals(Name, other.Name, StringComparison.Ordinal) && Colors.SequenceEqual(other.Colors);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name, StringComparer.Ordinal);
        foreach (var color in Colors)
            hash.Add(color);
        return hash.ToHashCode();
    }

    public override string ToString() => $"{Name} ({Colors.Count} colours)";
}
