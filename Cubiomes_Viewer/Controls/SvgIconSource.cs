namespace Cubiomes_Viewer.Controls;

/// <summary>The SVG files of an icon: its outlined form, and the filled form it takes while its control is active, if it has one.</summary>
public sealed class SvgIconSource
{
    /// <summary>The asset path of the outlined form, such as /Assets/Icons/home.svg.</summary>
    public string Outlined { get; set; } = string.Empty;

    /// <summary>The asset path of the filled form, or <see langword="null"/> when the icon has only one form.</summary>
    public string? Filled { get; set; }

    public override string ToString() => Outlined;
}
