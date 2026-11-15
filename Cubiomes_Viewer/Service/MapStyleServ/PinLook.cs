using Avalonia.Media;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>How a pin looks: plain, or with the glyph of a marker in its head, both tinted in that marker's colour.</summary>
public sealed class PinLook(string? icon, IBrush fill, IImage? glyph)
{
    /// <summary>The name of the marker whose glyph the pin shows, or <see langword="null"/> for a plain pin.</summary>
    public string? Icon { get; } = icon;

    /// <summary>The colour of the pin's rim and of its glyph.</summary>
    public IBrush Fill { get; } = fill;

    public IImage? Glyph { get; } = glyph;

    public override string ToString() => Icon ?? "Plain pin";
}
