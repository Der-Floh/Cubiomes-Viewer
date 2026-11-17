using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>A pin as a link shares it: its block, name and look, on the map of the link's seed and dimension.</summary>
public sealed record LinkPin(BlockPos Position, string? Name = null, string? Icon = null)
{
    public static LinkPin Of(MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new LinkPin(new BlockPos(pin.X, pin.Z), pin.Name, pin.Icon);
    }

    public MapPin ToPin(long seed, Dimension dimension)
        => new() { Seed = seed, Dimension = dimension, X = Position.X, Z = Position.Z, Name = Name, Icon = Icon };
}
