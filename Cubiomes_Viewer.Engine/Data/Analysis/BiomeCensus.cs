namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>The biomes of the samples of an area, or of a band of its rows.</summary>
public sealed class BiomeCensus
{
    public static BiomeCensus Empty { get; } = new() { Samples = 0, Tallies = [] };

    /// <summary>The samples that lie inside the area.</summary>
    public required int Samples { get; init; }

    /// <summary>One tally for each biome that occurs, by ascending biome id.</summary>
    public required BiomeTally[] Tallies { get; init; }

    /// <summary>
    /// Adds up the census of two parts of an area. The nearest sample of a biome is the nearer of the two, and the one
    /// of this census on a tie, so parts combined in the order of their rows find the same sample as one census.
    /// </summary>
    public BiomeCensus Add(BiomeCensus other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Dictionary<int, BiomeTally> tallies = Tallies.ToDictionary(tally => tally.Biome);
        foreach (var tally in other.Tallies)
            tallies[tally.Biome] = tallies.TryGetValue(tally.Biome, out var known) ? Merge(known, tally) : tally;

        return new BiomeCensus { Samples = Samples + other.Samples, Tallies = [.. tallies.Values.OrderBy(tally => tally.Biome)] };
    }

    private static BiomeTally Merge(BiomeTally first, BiomeTally second)
    {
        var nearest = second.NearestSquaredDistance < first.NearestSquaredDistance ? second : first;
        return first with { Samples = first.Samples + second.Samples, Nearest = nearest.Nearest, NearestSquaredDistance = nearest.NearestSquaredDistance };
    }
}
