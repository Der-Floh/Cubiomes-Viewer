using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Cubiomes_Viewer.Extensions;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls;

/// <summary>A pin drawn as the map draws it, filling its square: plain, or with the glyph of a marker in its head.</summary>
public sealed class PinIcon : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<PinIcon, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IImage?> GlyphProperty = AvaloniaProperty.Register<PinIcon, IImage?>(nameof(Glyph));

    public static readonly StyledProperty<IBrush?> FaceProperty = AvaloniaProperty.Register<PinIcon, IBrush?>(nameof(Face));

    public static readonly StyledProperty<IBrush?> OutlineProperty = AvaloniaProperty.Register<PinIcon, IBrush?>(nameof(Outline));

    static PinIcon()
    {
        AffectsRender<PinIcon>(FillProperty, GlyphProperty, FaceProperty, OutlineProperty);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IImage? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>The colour inside the pin's rim.</summary>
    public IBrush? Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    public IBrush? Outline
    {
        get => GetValue(OutlineProperty);
        set => SetValue(OutlineProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (Fill is not { } fill || Face is not { } face || Outline is not { } outline || side <= 0)
            return;

        var box = new Rect((Bounds.Width - side) / 2, (Bounds.Height - side) / 2, side, side);
        PinPainter.Draw(context, fill, Glyph, PinPainter.GetTip(box), side, this.GetPixelGrid(), face, outline);
    }
}
