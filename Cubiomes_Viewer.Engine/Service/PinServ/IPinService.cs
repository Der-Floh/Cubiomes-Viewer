using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.PinServ;

/// <summary>Keeps the pins the user puts on the maps of seeds in the settings, so they are there again next time.</summary>
public interface IPinService
{
    /// <summary>Raised after a pin was put down, changed or taken away.</summary>
    event Action? Changed;

    /// <summary>The pins on the map of one dimension of a seed, oldest first.</summary>
    IReadOnlyList<MapPin> GetPins(long seed, Dimension dimension);

    MapPin Add(long seed, Dimension dimension, BlockPos position);

    /// <summary>Shows the glyph of the named marker in the head of a pin, or nothing for <see langword="null"/>.</summary>
    void SetIcon(MapPin pin, string? icon);

    void Remove(MapPin pin);
}
