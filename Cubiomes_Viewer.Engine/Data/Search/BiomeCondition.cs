using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Biomes occur in the area, which is sampled every <see cref="Scale"/> blocks, or with <see cref="BiomeMatch.Share"/>
/// cover at least <see cref="MinShare"/> of it, judged from <see cref="Samples"/> points at random. A match is the
/// middle of the samples that have one of the biomes.
/// </summary>
public sealed record BiomeCondition : SearchCondition
{
    public const int DefaultScale = 16;
    public const int DefaultSamples = 400;
    public const int MinSamples = 10;
    public const int MaxSamples = 10_000;

    public required BiomeSet Biomes { get; init; }

    /// <summary>Whether one of the biomes is enough, each of them has to occur, or together they have to cover a share.</summary>
    public BiomeMatch Match { get; init; } = BiomeMatch.Any;

    public Dimension Dimension { get; init; } = Dimension.Overworld;

    /// <summary>Blocks between two samples: 1, 4, 16, 64 or 256.</summary>
    public int Scale { get; init; } = DefaultScale;

    /// <summary>The height the biomes are sampled at, in blocks.</summary>
    public int Y { get; init; } = ViewSettings.DefaultSampleHeight;

    /// <summary>The share of the area the biomes cover at least, above 0 and at most 1.</summary>
    public double MinShare { get; init; } = 0.5;

    /// <summary>How many random points judge the share. The points follow from the seed, so a seed always comes out the same.</summary>
    public int Samples { get; init; } = DefaultSamples;
}
