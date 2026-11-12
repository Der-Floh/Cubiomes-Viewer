using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// A climate parameter of the Overworld lies from <see cref="Min"/> to <see cref="Max"/> in the area, which is sampled
/// every <see cref="Scale"/> blocks. A match is the middle of the area, or with <see cref="ClimateMatch.Somewhere"/> the
/// middle of the samples in range.
/// </summary>
public sealed record ClimateCondition : SearchCondition
{
    public const int DefaultScale = 64;

    /// <summary>The largest value a parameter can take either way.</summary>
    public const double Limit = 3;

    public ClimateParameter Parameter { get; init; } = ClimateParameter.Continentalness;

    public double Min { get; init; } = 0.3;

    public double Max { get; init; } = 1;

    public ClimateMatch Match { get; init; } = ClimateMatch.Everywhere;

    /// <summary>Blocks between two samples: 4, 16, 64 or 256.</summary>
    public int Scale { get; init; } = DefaultScale;

    /// <summary>The height the parameter is sampled at, in blocks; only the depth changes with it.</summary>
    public int Y { get; init; } = ViewSettings.DefaultSampleHeight;
}
