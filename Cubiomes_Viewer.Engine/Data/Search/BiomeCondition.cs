using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Biomes occur in the area, which is sampled every <see cref="Scale"/> blocks. A match is the middle of the samples
/// that have one of the biomes.
/// </summary>
public sealed record BiomeCondition : SearchCondition
{
    public const int DefaultScale = 16;

    public required BiomeSet Biomes { get; init; }

    /// <summary>Whether one of the biomes is enough or each of them has to occur.</summary>
    public BiomeMatch Match { get; init; } = BiomeMatch.Any;

    public Dimension Dimension { get; init; } = Dimension.Overworld;

    /// <summary>Blocks between two samples: 1, 4, 16, 64 or 256.</summary>
    public int Scale { get; init; } = DefaultScale;

    /// <summary>The height the biomes are sampled at, in blocks.</summary>
    public int Y { get; init; } = ViewSettings.DefaultSampleHeight;
}
