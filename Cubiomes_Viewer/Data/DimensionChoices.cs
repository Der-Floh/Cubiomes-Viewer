using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>The dimensions every dimension picker offers, in the order of the game.</summary>
public static class DimensionChoices
{
    public static IReadOnlyList<DimensionChoice> Create(IMapStyleService styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        return
        [
            Create(styles, "Overworld", Dimension.Overworld),
            Create(styles, "Nether", Dimension.Nether),
            Create(styles, "End", Dimension.End),
        ];
    }

    private static DimensionChoice Create(IMapStyleService styles, string display, Dimension dimension)
        => new() { Display = display, Value = dimension, Marker = styles.GetDimensionMarker(dimension) };
}
