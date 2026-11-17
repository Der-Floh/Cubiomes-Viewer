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

    /// <summary>
    /// Puts down copies of pins, such as those a link shares, with their names and looks. A pin on a block that already holds a pin
    /// of its map is left out, so that the same pins put down again add nothing. Returns the pins put down.
    /// </summary>
    IReadOnlyList<MapPin> AddCopies(IEnumerable<MapPin> pins);

    /// <summary>Moves a pin to another block of its map.</summary>
    void Move(MapPin pin, BlockPos position);

    /// <summary>Names a pin; a name of nothing but white space takes the name away.</summary>
    void Rename(MapPin pin, string? name);

    /// <summary>Shows the glyph of the named marker in the head of a pin, or nothing for <see langword="null"/>.</summary>
    void SetIcon(MapPin pin, string? icon);

    void Remove(MapPin pin);
}
