using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Extensions;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls;

/// <summary>The badge of a marker, drawn on whole device pixels as the map draws it, so its outline stays crisp at every interface size.</summary>
public sealed class MarkerBadge : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty = AvaloniaProperty.Register<MarkerBadge, IBrush?>(nameof(Fill));

    public static readonly StyledProperty<IImage?> GlyphProperty = AvaloniaProperty.Register<MarkerBadge, IImage?>(nameof(Glyph));

    public static readonly StyledProperty<IBrush?> OutlineProperty = AvaloniaProperty.Register<MarkerBadge, IBrush?>(nameof(Outline));

    private PixelGrid _renderedPixels;

    static MarkerBadge()
    {
        AffectsRender<MarkerBadge>(FillProperty, GlyphProperty, OutlineProperty);
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

    public IBrush? Outline
    {
        get => GetValue(OutlineProperty);
        set => SetValue(OutlineProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        _renderedPixels = this.GetPixelGrid();
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (Fill is { } fill && Glyph is { } glyph && Outline is { } outline && side > 0)
            MarkerBadgePainter.Draw(context, fill, glyph, new Point(Bounds.Width / 2, Bounds.Height / 2), side, _renderedPixels, outline);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    /// <summary>Draws the badge again once moving or scaling it shifted it against the device pixels.</summary>
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (this.GetPixelGrid() != _renderedPixels)
            InvalidateVisual();
    }
}
