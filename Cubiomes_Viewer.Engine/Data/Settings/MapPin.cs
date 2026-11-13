using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>A pin the user put on the map of one dimension of a seed, plain or with the glyph of a marker in its head.</summary>
public sealed class MapPin
{
    public long Seed { get; set; }
    public Dimension Dimension { get; set; }
    public int X { get; set; }
    public int Z { get; set; }

    /// <summary>The name of the marker whose glyph the pin shows, such as a structure type's; <see langword="null"/> for a plain pin.</summary>
    public string? Icon { get; set; }

    public override string ToString() => $"Pin {Icon ?? "plain"} at {X}, {Z} ({Dimension} of {Seed})";
}
