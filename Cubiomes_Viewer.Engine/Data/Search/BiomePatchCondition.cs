using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Patches of the biomes in the area, which is sampled every <see cref="Scale"/> blocks: groups of neighbouring samples
/// with one of the biomes that cover at least <see cref="MinChunks"/> chunks. Each patch is a match, found at its
/// middle; a patch that reaches beyond the area counts only the part inside it.
/// </summary>
public sealed record BiomePatchCondition : SearchCondition
{
    public const int DefaultScale = 16;
    public const int DefaultMinChunks = 100;

    public required BiomeSet Biomes { get; init; }

    public Dimension Dimension { get; init; } = Dimension.Overworld;

    /// <summary>The fewest chunks a patch covers, counted from its samples.</summary>
    public int MinChunks { get; init; } = DefaultMinChunks;

    /// <summary>Blocks between two samples: 4, 16, 64 or 256.</summary>
    public int Scale { get; init; } = DefaultScale;

    /// <summary>The height the biomes are sampled at, in blocks.</summary>
    public int Y { get; init; } = ViewSettings.DefaultSampleHeight;
}
