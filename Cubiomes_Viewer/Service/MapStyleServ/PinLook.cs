using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>How a pin looks: plain, or with the glyph of a marker in its head, both tinted in that marker's colour. The glyph changes with the marker style.</summary>
public sealed partial class PinLook : ObservableObject
{
    /// <summary>The name of the marker whose glyph the pin shows, or <see langword="null"/> for a plain pin.</summary>
    public string? Icon { get; }

    /// <summary>The colour of the pin's rim and of its glyph.</summary>
    public IBrush Fill { get; }

    [ObservableProperty]
    public partial IImage? Glyph { get; set; }

    public PinLook(string? icon, IBrush fill, IImage? glyph)
    {
        Icon = icon;
        Fill = fill;
        Glyph = glyph;
    }

    public override string ToString() => Icon ?? "Plain pin";
}
